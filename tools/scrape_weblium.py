#!/usr/bin/env python3
"""One-off exporter: uagreen.co (Weblium) -> migration/*.json + migration/media/.

Contract: backend/src/UaGreen.Site.Api/Infrastructure/Import/ImportRunner.cs (snake_case keys).
Run:  tools/.venv/Scripts/python -I tools/scrape_weblium.py [--refresh] [--selftest]
Downloaded HTML is cached under migration/raw/ (gitignored); --refresh ignores the cache.
"""
import argparse
import html as htmlmod
import json
import re
import sys
import time
from datetime import datetime
from pathlib import Path
from urllib.parse import quote, unquote, urljoin, urlparse

import requests
from bs4 import BeautifulSoup, Comment, NavigableString
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

BASE = "https://uagreen.co"
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "migration"
MEDIA = OUT / "media"
RAW = OUT / "raw"
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36 uagreen-migration/1.0"
DELAY = 0.3

session = requests.Session()
session.headers["User-Agent"] = UA
session.mount("https://", HTTPAdapter(max_retries=Retry(total=3, backoff_factor=1, status_forcelist=[500, 502, 503, 504])))
URLS = {}  # url -> http status, for urls.json
REFRESH = False


# ── HTTP ──────────────────────────────────────────────────────────────────────
def get(url):
    """Cached GET of a text resource -> (status, text)."""
    key = RAW / (quote(url.split("://", 1)[1], safe="") + ".html")
    st = key.with_suffix(".status")
    if not REFRESH and key.exists() and st.exists():
        return int(st.read_text()), key.read_text(encoding="utf-8")
    r = session.get(url, timeout=30)
    time.sleep(DELAY)
    key.write_text(r.text, encoding="utf-8")
    st.write_text(str(r.status_code))
    return r.status_code, r.text


def page(path):
    """Soup of a site page (even for 404 pages: /404 is content too)."""
    status, text = get(BASE + path)
    URLS[BASE + path] = status
    return BeautifulSoup(text, "html.parser")


def sitemap_urls(url):
    return re.findall(r"<loc>\s*(\S+?)\s*</loc>", get(url)[1])


# ── media ─────────────────────────────────────────────────────────────────────
EXT = {"image/jpeg": "jpg", "image/png": "png", "image/webp": "webp", "image/gif": "gif", "image/svg+xml": "svg", "application/pdf": "pdf"}
media_files = {}  # weblium id -> file name


def media_source(src):
    """Weblium media URL -> (id, URL of the largest rendition), or None for foreign hosts."""
    u = urlparse(urljoin(BASE, src))
    parts = u.path.strip("/").split("/")
    if u.netloc == "res2.weblium.site" and len(parts) >= 3:  # res/<site>/<id>[_optimized[_W_cWxH-XxY]]
        rid = parts[2].split("_")[0]
        return rid, f"https://{u.netloc}/res/{parts[1]}/{rid}_optimized"
    if u.netloc == "e-c.storage.googleapis.com" and len(parts) >= 2:  # res/<uuid>/<size|original>
        return parts[1], f"https://{u.netloc}/res/{parts[1]}/original"
    if u.netloc == "blog-api.getblog.app" and parts:  # pv/<id>?W=.. | customer/resource/<hash>?w=..
        return parts[-1], f"https://{u.netloc}/{u.path.strip('/')}"
    return None


def media(src):
    """Download the largest rendition once (named <id>.<ext> by content type); return the file name."""
    ms = media_source(src)
    if not ms:
        return None
    rid, url = ms
    if rid in media_files:
        return media_files[rid]
    found = next((f for f in MEDIA.glob(rid + ".*") if f.stat().st_size), None)
    if found and not REFRESH:
        media_files[rid] = found.name
        return found.name
    for u in dict.fromkeys([url, url.replace("_optimized", "")]):  # _optimized is the largest; bare id as fallback
        r = session.get(u, timeout=60)
        time.sleep(DELAY)
        if r.ok and r.content:
            break
    else:
        print(f"  ! media failed: {src}")
        return None
    name = f"{rid}.{EXT.get(r.headers.get('Content-Type', '').split(';')[0].strip(), 'bin')}"
    (MEDIA / name).write_bytes(r.content)
    media_files[rid] = name
    return name


# ── Weblium markup -> clean HTML ──────────────────────────────────────────────
# The DOM is flattened into lines of text runs {t, bold, italic, href}; images and tables are
# their own blocks. Lines then become p / h2 / h3 / li by hint, bullet prefix or bold-only text.
SKIP_TAGS = {"script", "style", "svg", "template", "form", "button", "noscript", "input", "select", "textarea", "iframe", "source"}
SKIP_CLASSES = {"w-icon", "w-form-wrapper", "w-form-modal", "js-templates-container", "tabs-container", "w-logo", "w-socials"}
BLOCK_TAGS = {"p", "div", "section", "article", "header", "footer", "li", "ul", "ol", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "figure", "tr"}
HEAD2 = ("ui-blockTitle", "ui-heroTitle", "ui-headingLg")
HEAD3 = ("ui-heading", "ui-subheading")
BULLET = re.compile(r"^[●•\-–—]\s*")
DROP = re.compile(r"^(//\s*\d+\.?\s*//|(Головна|Home)\s*>.*)$")  # Weblium decorations, breadcrumbs
MUSTACHE = re.compile(r"\{%.*?%\}")


class Conv:
    def __init__(self):
        self.blocks, self.line, self.hints = [], [], [None]

    def flush(self):
        if any(r["t"].strip() for r in self.line):
            self.blocks.append(("line", self.line, self.hints[-1]))
        self.line = []

    def walk(self, node, st):
        if isinstance(node, Comment):
            return
        if isinstance(node, NavigableString):
            t = re.sub(r"[\sㅤ﻿\xa0]+", " ", MUSTACHE.sub("", str(node)))
            if t:
                self.line.append(dict(st, t=t))
            return
        cls = set(node.get("class", []))
        if node.name in SKIP_TAGS or cls & SKIP_CLASSES:
            return
        if node.name == "br":
            self.line.append(dict(st, t="\n"))
            return
        if node.name in ("picture", "img"):
            img = node if node.name == "img" else node.find("img")
            srcs = [s.get("srcset", "").split()[0] for s in node.find_all("source") if s.get("srcset")]
            src = (srcs or [img and (img.get("src") or img.get("data-src"))])[0]
            if src and not (img and (img.get("alt") == "icon" or "js-preview-img" in img.get("class", []))):
                self.flush()
                f = media(src)
                if f:
                    self.blocks.append(("img", f, None))
            return
        if node.name == "table":
            self.flush()
            self.blocks.append(("table", node, None))
            return
        style = node.get("style", "").replace(" ", "")
        st = dict(st)
        if node.name in ("b", "strong") or "font-weight:bold" in style or "font-weight:700" in style:
            st["bold"] = True
        if node.name in ("i", "em") or "font-style:italic" in style:
            st["italic"] = True
        if node.name == "a" and node.get("href"):
            href = urljoin(BASE, node["href"])
            st["href"] = href[len(BASE):] if href.startswith(BASE + "/") else href
        hint = None
        if node.name in ("h1", "h2") or any(c in cls for c in HEAD2):
            hint = "h2"
        elif node.name in ("h3", "h4") or any(c in cls for c in HEAD3):
            hint = "h3"
        elif "w-icon-list__item" in cls or (node.name == "li" and not node.find(class_="w-text-block")):  # Weblium collections are ul/li too
            hint = "li"
        block = node.name in BLOCK_TAGS or "w-text-block" in cls or hint
        if block:
            self.flush()
        if hint:
            self.hints.append(hint)
        for ch in node.children:
            self.walk(ch, st)
        if block:
            self.flush()
        if hint:
            self.hints.pop()


def render_runs(runs):
    segs = [[]]
    for r in runs:
        if r.get("br"):
            segs.append([])
        elif r["t"]:
            segs[-1].append(r)
    return "<br>".join(filter(None, (render_seg(s) for s in segs)))


def render_seg(runs):
    if runs:
        runs[0] = dict(runs[0], t=runs[0]["t"].lstrip())
        runs[-1] = dict(runs[-1], t=runs[-1]["t"].rstrip())
    merged = []
    for r in runs:
        key = (r.get("bold"), r.get("italic"), r.get("href"))
        if merged and merged[-1][0] == key:
            merged[-1][1] += r["t"]
        else:
            merged.append([key, r["t"]])
    out = ""
    for (bold, italic, href), t in merged:
        if not t:
            continue
        s = htmlmod.escape(t, quote=False)
        if italic:
            s = f"<em>{s}</em>"
        if bold:
            s = f"<strong>{s}</strong>"
        if href:
            s = f'<a href="{htmlmod.escape(href)}">{s}</a>'
        out += s
    return out


def clean_table(tag):
    rows = []
    for tr in tag.find_all("tr"):
        cells = []
        for td in tr.find_all(["td", "th"]):
            c = Conv()
            c.walk(td, {})
            c.flush()
            cells.append(f"<{td.name}>{to_html(items_of(c.blocks))}</{td.name}>")
        rows.append("<tr>" + "".join(cells) + "</tr>")
    return "".join(rows)


def items_of(blocks, all_bold=False):
    """Blocks -> [(tag, inner_html)]. all_bold: the whole fragment is bold (EN vacancy pages), so bold
    carries no meaning and only very short lines count as headings."""
    items = []
    for kind, payload, hint in blocks:
        if kind == "img":
            items.append(("p", f'<img src="/media/{payload}">'))
            continue
        if kind == "table":
            items.append(("table", clean_table(payload)))
            continue
        if hint in ("h2", "h3"):  # a <br> inside a heading stays inside it
            payload = [dict(r, t="", br=True) if r["t"] == "\n" else r for r in payload]
        line = []
        for r in payload + [dict(t="\n")]:
            if r["t"] != "\n":
                line.append(r)
                continue
            text = "".join(x["t"] for x in line).strip()
            if text and not DROP.match(text):
                tag = hint
                if BULLET.match(text) and hint in (None, "li") and not re.search(r"\s[-–—]$", text):
                    tag = "li"
                    first = next(x for x in line if x["t"].strip())
                    first["t"] = BULLET.sub("", first["t"].lstrip(), 1)
                elif (tag is None and len(text) <= 80 and all(x.get("bold") and not x.get("href") for x in line if x["t"].strip())
                      and (not all_bold or len(text.split()) <= 4)):
                    tag = "h3"
                if tag in ("h2", "h3") or all_bold:
                    line = [dict(x, bold=False) for x in line]
                inner = render_runs(line)
                if inner:
                    items.append((tag or "p", inner))
            line = []
    return items


def to_html(items):
    out, in_list = [], False
    for tag, inner in items:
        if (tag == "li") != in_list:
            out.append("<ul>" if tag == "li" else "</ul>")
            in_list = tag == "li"
        out.append(f"<{tag}>{inner}</{tag}>")
    if in_list:
        out.append("</ul>")
    return "".join(out) or None


def convert(node):
    """Any soup node (or HTML string) -> [(tag, inner_html)]."""
    if isinstance(node, str):
        node = BeautifulSoup(node, "html.parser")
    c = Conv()
    c.walk(node, {})
    c.flush()
    runs = [r for b in c.blocks if b[0] == "line" and b[2] not in ("h2", "h3") for r in b[1] if r["t"].strip()]
    return items_of(c.blocks, all_bold=bool(runs) and all(r.get("bold") for r in runs))


def strip_tags(s):
    return htmlmod.unescape(re.sub(r"<[^>]+>", "", s or "")).strip()


def block(soup, anchor):
    """Weblium block wrapper that holds the given anchor id (cover, services, ...)."""
    a = soup.find("div", id=anchor)
    return a.find_parent(class_="block-wrapper") if a else None


def en_or_none(en, uk):
    """*_en is null when missing or just the untranslated uk value."""
    return en if en and en != uk else None


TR = {"а": "a", "б": "b", "в": "v", "г": "h", "ґ": "g", "д": "d", "е": "e", "є": "ye", "ж": "zh", "з": "z", "и": "y", "і": "i", "ї": "yi", "й": "i",
      "к": "k", "л": "l", "м": "m", "н": "n", "о": "o", "п": "p", "р": "r", "с": "s", "т": "t", "у": "u", "ф": "f", "х": "kh", "ц": "ts", "ч": "ch",
      "ш": "sh", "щ": "shch", "ь": "", "ю": "yu", "я": "ya", "'": "", "’": "", "ʼ": ""}


def slugify(s):
    return re.sub(r"[^a-z0-9]+", "-", "".join(TR.get(c, c) for c in s.lower())).strip("-")


# ── shop ──────────────────────────────────────────────────────────────────────
def appdata(soup):
    m = re.search(r"window\.__APP_DATA__\s*=\s*(\{.*?\});?\s*</script>", str(soup), re.S)
    return json.loads(m.group(1)) if m else None


def shop_links(soup, prefix):
    seen = []
    for a in soup.select("a[href]"):
        path = urlparse(urljoin(BASE, a["href"])).path
        m = re.fullmatch(prefix + r"([^/]+)", path)
        if m and m.group(1) not in seen:
            seen.append(m.group(1))
    return seen


def option_deltas(product):
    """Weblium prices every variant separately; derive per-value deltas against the default value
    from variant pairs that differ only in that option (fallback: cheapest variant with the value)."""
    variants = product.get("variants") or [product["variant"]]
    base = min(variants, key=lambda v: v["sort_order"])
    vals = lambda v: tuple((x["name"], x["value"]) for x in v.get("values", []))
    by_vals = {vals(v): v for v in variants}
    out = []
    for o in product.get("options", []):
        default = o["values"][0]
        values = []
        for val in o["values"]:
            delta = 0
            if val != default:
                cands = []
                for v in variants:
                    if dict(vals(v)).get(o["name"]) != val:
                        continue
                    v0 = by_vals.get(tuple((n, default if n == o["name"] else x) for n, x in vals(v)))
                    if v0:
                        cands.append(v["amount"] - v0["amount"])
                with_val = [v["amount"] for v in variants if dict(vals(v)).get(o["name"]) == val]
                delta = cands[0] if cands else (min(with_val) - base["amount"] if with_val else 0)
            values.append((val, delta / 100))
        out.append((o["name"], values))
    return out


def split_faq(items):
    for i, (tag, inner) in enumerate(items):
        if strip_tags(inner).strip(":").upper() == "FAQ":
            return items[:i], items[i + 1:]
    return items, []


def specs_table(attrs):
    if not attrs:
        return None
    return "<table>" + "".join(f"<tr><th>{htmlmod.escape(a['name'])}</th><td>{htmlmod.escape(str(a['value']))}</td></tr>" for a in attrs) + "</table>"


def scrape_shop(featured_ids):
    shop, shop_en = page("/shop"), page("/en/shop")
    cat_labels = {a["href"].rstrip("/").rsplit("/", 1)[-1]: a for a in shop.select("a[href*='/shop/cat/']")}
    en_labels = {a["href"].rstrip("/").rsplit("/", 1)[-1]: a.get_text(" ", strip=True) for a in shop_en.select("a[href*='/shop/cat/']")}
    categories = []
    for n, slug in enumerate(shop_links(shop, "/shop/cat/"), 1):
        a = cat_labels[slug]
        img = a.find("img")
        name = a.get_text(" ", strip=True)
        categories.append({"slug": slug, "name_uk": name, "name_en": en_or_none(en_labels.get(slug), name), "sort": n,
                           "image": media(img["src"]) if img and img.get("src") else None})
        page(f"/shop/cat/{slug}"), page(f"/en/shop/cat/{slug}")  # status only, for urls.json
    products = []
    for n, slug in enumerate(shop_links(shop, "/shop/"), 1):
        if slug == "cat":
            continue
        uk, en = appdata(page(f"/shop/{slug}")), appdata(page(f"/en/shop/{slug}")) or {}
        p, pe = uk["product"], en.get("product", {})
        var = p["variant"]
        desc, faq = split_faq(convert(p.get("description") or ""))
        desc_en, faq_en = split_faq(convert(pe.get("description") or ""))
        cat = next((b["url"].rstrip("/").rsplit("/", 1)[-1] for b in uk.get("breadcrumbs", []) if "/shop/cat/" in b["url"]), None)
        short = lambda d: next((strip_tags(i) for t, i in convert(d.get("short_description") or "") if t == "p"), None)
        images = sorted((r for r in p.get("resources", []) if (r.get("mime_type") or "").startswith("image/")), key=lambda r: r["sort_order"])
        opts_en = {o["name"]: o["values"] for o in pe.get("options", [])}
        options = []
        for name, values in option_deltas(p):
            en_vals = opts_en.get(name, [])
            options.append({"name_uk": name, "name_en": None,
                            "values": [{"value_uk": v, "value_en": en_or_none(en_vals[i] if i < len(en_vals) else None, v), "price_delta": d}
                                       for i, (v, d) in enumerate(values)]})
        products.append({
            "slug": slug, "sku": str(var.get("code")) if var.get("code") is not None else None, "category_slug": cat,
            "name_uk": p["name"], "name_en": en_or_none(pe.get("name"), p["name"]),
            "short_desc_uk": short(p), "short_desc_en": en_or_none(short(pe), short(p)),
            "description_html_uk": to_html(desc), "description_html_en": en_or_none(to_html(desc_en), to_html(desc)),
            "specs_html_uk": specs_table(p.get("attributes")), "specs_html_en": en_or_none(specs_table(pe.get("attributes")), specs_table(p.get("attributes"))),
            "faq_html_uk": to_html(faq), "faq_html_en": en_or_none(to_html(faq_en), to_html(faq)),
            "price": var["amount"] / 100, "old_price": var["amount_old"] / 100 if var.get("amount_old") else None,
            "in_stock": var.get("status") == "in_stock", "is_featured": p["slug_id"] in featured_ids, "sort": n,
            "seo_title_uk": p.get("meta_seo_title") or None, "seo_title_en": en_or_none(pe.get("meta_seo_title"), p.get("meta_seo_title")),
            "seo_desc_uk": p.get("meta_description") or None, "seo_desc_en": en_or_none(pe.get("meta_description"), p.get("meta_description")),
            "images": [{"file": f, "alt": p["name"]} for r in images if (f := media(r["image_url"]))],
            "files": [{"file": f, "title": a.get("name") or a.get("title") or f} for a in p.get("attachments", []) if (f := media(a.get("url", "")))],
            "options": options,
            "source_url_uk": f"{BASE}/shop/{slug}", "source_url_en": f"{BASE}/en/shop/{slug}",
        })
    return categories, products


# ── blog ──────────────────────────────────────────────────────────────────────
def scrape_blog():
    posts = []
    for url in sitemap_urls(BASE + "/blog/sitemap.xml"):
        path = urlparse(url).path
        soup = page(path)
        art = soup.select_one("section.w-article")
        cover = art.select_one("img.w-article__preview")
        date = art.select_one("time.w-article__date")
        date = (date.get("datetime") or date.get_text(strip=True)) if date else None
        title = art.select_one("h1").get_text(" ", strip=True)
        author = art.select_one(".w-article__author-name")
        author = author.get_text(strip=True) if author else None
        body = art.select_one("section.w-article__content")
        for h in body.select("header.w-article__content-header"):
            h.decompose()
        items = convert(body)
        seo_title = re.sub(r"\s*\|[^|]*$", "", (soup.title.string or "").strip())  # drop Weblium's "| UA GREEN" / "| Homeware" suffix
        seo_desc = (soup.find("meta", attrs={"name": "description"}) or {}).get("content") or None
        posts.append({
            "slug": path.rsplit("/", 1)[-1], "title_uk": title,
            "excerpt_uk": next((strip_tags(i) for t, i in items if t == "p" and "<img" not in i), None),
            "body_html_uk": to_html(items) or "",
            "cover": media(cover["src"]) if cover and cover.get("src") else None,
            "author": author,
            "published_at": datetime.strptime(date, "%d %b %Y").date().isoformat() if date else None,
            "seo_title": en_or_none(seo_title, title), "seo_desc": seo_desc, "source_url": url,
        })
    return posts


# ── vacancies ─────────────────────────────────────────────────────────────────
SECTION = {"requirements": ("вимоги", "requirements"),
           "nice_to_have": ("бажані навички", "буде плюсом", "preferred skills", "would be a plus", "nice to have", "desired skills"),
           "conditions": ("умови роботи", "working conditions")}


def vacancy_sections(items):
    """Split body at h3 headings; known headings fill their field, the rest is appended to conditions."""
    secs, cur = [(None, [])], None
    for tag, inner in items:
        if tag == "h3":
            secs.append((strip_tags(inner), []))
        else:
            secs[-1][1].append((tag, inner))
    fields, extra = {}, []
    for head, its in secs:
        key = next((k for k, names in SECTION.items() if head and head.lower().strip(" :") in names), None)
        if key and key not in fields:
            fields[key] = to_html(its)
        elif its:
            extra.append((f"<h3>{htmlmod.escape(head)}</h3>" if head else "") + (to_html(its) or ""))
    if extra:  # ponytail: contract has no field for Обов'язки / Що ми пропонуємо / Процес найму — kept under conditions
        fields["conditions"] = (fields.get("conditions") or "") + "".join(extra)
    return fields


def vacancy_detail(path):
    soup = page(path)
    blk = block(soup, "careers")
    title = blk.select_one(".ui-blockTitle").get_text(" ", strip=True)
    sub = [s.strip() for s in blk.select_one(".ui-subtitle").get_text(" ", strip=True).split("•")] if blk.select_one(".ui-subtitle") else []
    content = blk.select_one(".w-section__content")
    return {"title": title, "experience": sub[0] if sub else None, "employment": sub[1] if len(sub) > 1 else None,
            "language": " • ".join(sub[2:]) or None, **vacancy_sections(convert(content) if content else [])}


def scrape_vacancies():
    soup = page("/vacancies")
    page("/en/vacancies")
    tabs = soup.select_one(".tabs-container")
    departments = [t.get_text(" ", strip=True) for t in tabs.find(class_=re.compile(r"^tabs-nav_")).select(".ui-heading")]
    cards = []  # (old_path, department, employment, city)
    for dep, coll in zip(departments, tabs.select_one(".tabs-items-wrapper").find_all("div", class_="collection", recursive=False)):
        for card in coll.select(".w-grid__item"):
            caps = [c.get_text(" ", strip=True) for c in card.select(".ui-caption")]
            a = card.select_one("a[href]")
            if a:
                cards.append((urlparse(urljoin(BASE, a["href"])).path, dep, caps[0] if caps else None, caps[1] if len(caps) > 1 else None))
    listed = {c[0] for c in cards}
    for url in sitemap_urls(BASE + "/sitemap_pages.xml"):  # vacancies not linked from /vacancies -> inactive
        p = urlparse(url).path
        if p.count("/") == 1 and p not in listed and p not in ("/", "/about-us", "/contacts", "/blog", "/404", "/umovi-vikoristannya-saytu", "/vacancies", "/oplata", "/en"):
            cards.append((p, None, None, None))
    vacancies, slugs = [], set()
    for old, dep, employment, city in cards:
        uk, en = vacancy_detail(old), vacancy_detail("/en" + old)
        slug = old.strip("/") if not re.search(r"-\d+$", old) else slugify(uk["title"])
        while slug in slugs:
            slug += "-2"
        slugs.add(slug)
        vacancies.append({
            "slug": slug, "old_path_uk": old, "old_path_en": "/en" + old,
            "title_uk": uk["title"], "title_en": en_or_none(en["title"], uk["title"]),
            "department": dep, "employment_type": employment or uk["employment"], "city": city or "Львів",
            "experience": uk["experience"], "language": uk["language"],
            "requirements_html_uk": uk.get("requirements"), "requirements_html_en": en_or_none(en.get("requirements"), uk.get("requirements")),
            "nice_to_have_html_uk": uk.get("nice_to_have"), "nice_to_have_html_en": en_or_none(en.get("nice_to_have"), uk.get("nice_to_have")),
            "conditions_html_uk": uk.get("conditions"), "conditions_html_en": en_or_none(en.get("conditions"), uk.get("conditions")),
            "is_active": dep is not None,
        })
    return vacancies


# ── content pages + settings ──────────────────────────────────────────────────
PAGES = [  # key, uk path, en path
    ("page.about", "/about-us", "/en/about-us-1"),
    ("page.contacts", "/contacts", "/en/contacts-1"),
    ("page.oplata", "/oplata", "/en/oplata"),
    ("page.terms", "/umovi-vikoristannya-saytu", "/en/umovi-vikoristannya-saytu-1"),
    ("page.404", "/404", "/en/404-1"),
    ("page.vacancies", "/vacancies", "/en/vacancies"),
]
HOME = [("home.hero", "cover"), ("home.services", "services"), ("home.projects", "projects"), ("home.testimonials", "testimonials"),
        ("home.about", "about-us"), ("home.passion", "passion-innovations"), ("home.faq", "faq")]
CHROME = {"header", "footer", "fixed-block"}


def content_row(key, lang, nodes):
    items = [it for n in nodes for it in convert(n)]
    title = next((i for t, i in items if t == "h2"), None)
    if title:
        items.remove(("h2", title))
        title = re.sub(r"\s*[—–-]{2,}\s*", " ", strip_tags(title.replace("<br>", " "))).strip()  # decorative dash runs
    return {"key": key, "lang": lang, "title": title, "body_html": to_html(items) or ""}


def scrape_content(home, home_en):
    rows = []
    for key, anchor in HOME:
        for lang, soup in (("uk", home), ("en", home_en)):
            b = block(soup, anchor)
            if b:
                rows.append(content_row(key, lang, [b]))
    for key, uk, en in PAGES:
        for lang, path in (("uk", uk), ("en", en)):
            soup = page(path)
            blocks = [b for b in soup.select("div.block-wrapper") if not (b.find("div", id=True) or {}).get("id") in CHROME]
            if blocks:
                rows.append(content_row(key, lang, blocks))
    return rows


def decode_email(s):
    """Weblium obfuscates addresses as the reversed string with %40 for '@'."""
    s = s.strip()
    return unquote(s)[::-1] if "%40" in s else s


def scrape_settings(home, home_en, vacancy_path):
    raw = str(home)
    apply_link = page(vacancy_path).select_one("a[href*='forms.gle']")
    phones, emails, socials = [], [], {}
    for soup in (home, page("/contacts")):
        for a in soup.select("a[href^='tel:']"):
            p = re.sub(r"[^\d+]", "", a["href"][4:])
            if p not in phones:
                phones.append(p)
        for a in soup.select("a[href^='mailto:']"):
            e = decode_email(a["href"][7:])
            if e not in emails:
                emails.append(e)
        for t in soup.find_all(string=re.compile(r"^\s*\S+%40\S+\s*$")):
            e = decode_email(str(t))
            if e not in emails:
                emails.append(e)
    for e in re.findall(r"[\w.+-]+@uagreen\.co\b", raw):
        if e not in emails:
            emails.append(e)
    for m in re.finditer(r"https?://(?:www\.)?(instagram|tiktok|linkedin|youtube|facebook)\.com/[A-Za-z0-9_@./-]*[A-Za-z0-9_]", raw):
        if not m.group(0).endswith("facebook.com/tr"):  # pixel endpoint, not a profile
            socials.setdefault(m.group(1), m.group(0))

    def footer_addr(soup):
        out = {}
        for p in block(soup, "footer").select(".w-icon-list .ui-caption"):
            label, _, value = p.get_text(" ", strip=True).partition(":")
            if value:
                out[label.strip().lower()] = value.strip()
        return out

    addr, addr_en = footer_addr(home), footer_addr(home_en)
    pick = lambda d, *keys: next((d[k] for k in keys if k in d), None)
    meta = lambda s, n: (s.find("meta", attrs={"name": n}) or {}).get("content") or None
    title_uk, title_en = (home.title.string or "").strip(), (home_en.title.string or "").strip()
    return {
        "phones": phones, "emails": emails,
        "address": pick(addr, "шоурум", "склад"), "address_en": pick(addr_en, "showroom", "warehouse"),
        "address_office": pick(addr, "головний офіс"), "address_office_en": pick(addr_en, "head office"),
        "socials": socials,
        "vacancy_apply_url": apply_link["href"] if apply_link else None,
        "ga4_id": next(iter(re.findall(r"gtag/js\?id=(G-[A-Z0-9]+)", raw)), None),
        "google_ads_id": next(iter(re.findall(r"\b(AW-\d+)\b", raw)), None),
        "fb_pixel_id": next(iter(re.findall(r"fbq\('init',\s*'(\d+)'", raw)), None),
        "site_title_uk": title_uk, "site_title_en": en_or_none(title_en, title_uk),
        "site_description_uk": meta(home, "description"), "site_description_en": en_or_none(meta(home_en, "description"), meta(home, "description")),
    }


# ── main ──────────────────────────────────────────────────────────────────────
def dump(name, data):
    (OUT / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{name}: {len(data)} rows" if isinstance(data, list) else f"{name}: written")


def main():
    global REFRESH
    ap = argparse.ArgumentParser()
    ap.add_argument("--refresh", action="store_true", help="ignore cached HTML/media")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    REFRESH = args.refresh
    for d in (OUT, MEDIA, RAW):
        d.mkdir(parents=True, exist_ok=True)
    (OUT / ".gitignore").write_text("media/\nraw/\n")

    home, home_en = page("/"), page("/en")
    featured = block(home, "featured-items")
    featured_ids = {json.loads(el["data-value"]).get("productId") for el in featured.select(".js-product[data-value]")} if featured else set()
    categories, products = scrape_shop(featured_ids)
    dump("categories.json", categories)
    dump("products.json", products)
    dump("blog_posts.json", scrape_blog())
    vacancies = scrape_vacancies()
    dump("vacancies.json", vacancies)
    dump("content.json", scrape_content(home, home_en))
    dump("settings.json", scrape_settings(home, home_en, vacancies[0]["old_path_uk"]))

    redirects = [{"from": v["old_path_uk"], "to": f"/vacancies/{v['slug']}"} for v in vacancies]
    redirects += [{"from": v["old_path_en"], "to": f"/en/vacancies/{v['slug']}"} for v in vacancies]
    redirects += [{"from": en, "to": "/en" + uk} for _, uk, en in PAGES if en != "/en" + uk]
    dump("redirects.json", redirects)

    for index in sitemap_urls(BASE + "/sitemap.xml"):  # every old URL gets a status, for the launch checklist
        for url in sitemap_urls(index):
            if url not in URLS:
                page(urlparse(url).path or "/")
    page("/en/blog")  # linked nowhere, but worth a status on the checklist
    dump("urls.json", [{"url": u, "status": s} for u, s in sorted(URLS.items())])
    files = [f for f in MEDIA.iterdir() if f.is_file()]
    print(f"media: {len(files)} files, {sum(f.stat().st_size for f in files) / 1e6:.1f} MB")


def selftest():
    frag = ('<p class="text_1L- ui-text"><span class="w-text-block"><span style="font-weight:bold">Вимоги</span></span>'
            '<span class="w-text-block">● один {%name%}</span><span class="w-text-block">● <a href="https://uagreen.co/shop">два</a></span>'
            '<br/><span class="w-text-block">Текст <em>курсив</em></span></p><h2 class="x">Заголовок</h2>')
    got = to_html(convert(frag))
    assert got == '<h3>Вимоги</h3><ul><li>один</li><li><a href="/shop">два</a></li></ul><p>Текст <em>курсив</em></p><h2>Заголовок</h2>', got
    assert media_source("//res2.weblium.site/res/66a0/67621a54_optimized_1200_c1200x669-0x0") == ("67621a54", "https://res2.weblium.site/res/66a0/67621a54_optimized")
    assert media_source("//e-c.storage.googleapis.com/res/0530-aefc/112") == ("0530-aefc", "https://e-c.storage.googleapis.com/res/0530-aefc/original")
    assert media_source("https://blog-api.getblog.app/pv/67883c6a?W=1170")[1] == "https://blog-api.getblog.app/pv/67883c6a"
    assert decode_email("oc.neergau%40olleh") == "hello@uagreen.co" and decode_email("oc.neergau%40rukonyv.iiskelo") == "oleksii.vynokur@uagreen.co"
    bold = '<p><span class="w-text-block"><b>Requirements<br>● one<br>● two</b></span></p><h2 class="ui-blockTitle">A<br>B</h2>'
    assert to_html(convert(bold)) == "<h3>Requirements</h3><ul><li>one</li><li>two</li></ul><h2>A<br>B</h2>", to_html(convert(bold))
    assert slugify("Молодший інженер-конструктор") == "molodshyi-inzhener-konstruktor"
    p = {"options": [{"name": "M", "values": ["a", "b"]}], "variant": {}, "variants": [
        {"sort_order": 1, "amount": 100, "values": [{"name": "M", "value": "a"}]}, {"sort_order": 2, "amount": 250, "values": [{"name": "M", "value": "b"}]}]}
    assert option_deltas(p) == [("M", [("a", 0), ("b", 1.5)])]
    print("selftest ok")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    main()
