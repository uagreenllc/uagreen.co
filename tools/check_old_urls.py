#!/usr/bin/env python3
"""Every URL of the old Weblium site must still answer on the new one.

    python tools/check_old_urls.py http://localhost:3000

Reads migration/urls.json (the old sitemap, uk + en), requests each path against BASE_URL,
follows redirects by hand so the chain is printed, and exits 1 if any final status is not
200 — except the old 404 pages, which must end in 404.
"""
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

EXPECT_404 = {"/404", "/en/404", "/en/404-1"}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def fetch(url: str) -> tuple[int, str | None]:
    req = urllib.request.Request(url, headers={"Accept": "text/html", "User-Agent": "uagreen-url-audit"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, None
    except urllib.error.HTTPError as e:
        return e.code, e.headers.get("Location")


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    base = sys.argv[1].rstrip("/")
    urls = json.loads((Path(__file__).resolve().parent.parent / "migration" / "urls.json").read_text("utf-8"))
    failed = 0
    for row in urls:
        path = urllib.parse.urlsplit(row["url"]).path or "/"
        chain, cur = [], path
        for _ in range(5):
            status, location = fetch(base + cur)
            chain.append(f"{cur} {status}")
            if status in (301, 302, 307, 308) and location:
                cur = urllib.parse.urlsplit(location).path
                continue
            break
        ok = status == (404 if path in EXPECT_404 else 200)
        failed += not ok
        print(("ok   " if ok else "FAIL ") + " → ".join(chain))
    print(f"\n{len(urls) - failed}/{len(urls)} ok")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
