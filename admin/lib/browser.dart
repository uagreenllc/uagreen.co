import 'dart:async';
import 'dart:js_interop';
import 'dart:typed_data';

import 'package:flutter/widgets.dart';
import 'package:web/web.dart' as web;

// Web-only bits (the admin is never built for anything else).

String? storageGet(String key) => web.window.localStorage.getItem(key);

void storageSet(String key, String? value) =>
    value == null ? web.window.localStorage.removeItem(key) : web.window.localStorage.setItem(key, value);

class PickedFile {
  PickedFile(this.name, this.type, this.bytes);
  final String name, type;
  final Uint8List bytes;
}

/// Native file dialog; null when cancelled.
Future<PickedFile?> pickFile(String accept) async {
  final input = web.HTMLInputElement()
    ..type = 'file'
    ..accept = accept;
  final picked = Future.any([
    web.EventStreamProviders.changeEvent.forTarget(input).first,
    const web.EventStreamProvider<web.Event>('cancel').forTarget(input).first,
  ]);
  input.click();
  await picked;
  final f = input.files?.item(0);
  if (f == null) return null;
  final buf = await f.arrayBuffer().toDart;
  return PickedFile(f.name, f.type, buf.toDart.asUint8List());
}

void saveBytes(Uint8List bytes, String name) {
  final url = web.URL.createObjectURL(web.Blob([bytes.toJS].toJS));
  web.HTMLAnchorElement()
    ..href = url
    ..download = name
    ..click();
  Timer(const Duration(minutes: 1), () => web.URL.revokeObjectURL(url));
}

void openUrl(String url) => web.window.open(url, '_blank');

extension on web.Document {
  external bool queryCommandState(String command); // dropped from package:web, still in every browser
}

const _css = '''
html{height:100%}body{margin:0;padding:20px 24px;min-height:calc(100% - 40px);font:17px/1.6 system-ui,sans-serif;color:#44524a}
h1,h2,h3{color:#1f2a22;line-height:1.2;margin:1.2em 0 .5em}h2{font-size:28px;font-weight:600}h3{font-size:22px;font-weight:600}
p{margin:0 0 1em}a{color:#3f6b3a}img{max-width:100%;height:auto;border-radius:8px}
blockquote{margin:1em 0;padding-left:16px;border-left:3px solid #c8d3c4}ul,ol{padding-left:1.4em}''';

/// Visual HTML editor: the browser's own rich-text editing (designMode) in an iframe.
/// Sandboxed without allow-scripts, so a post body can't run code in the admin's
/// session (allow-same-origin only lets the admin reach in; a fully opaque origin
/// blocked the images). The `<base>` (API origin) resolves `/media/...` links.
class RichDoc {
  RichDoc({required this.onChange, required this.onSelect, required this.onWheel});

  final void Function(String html) onChange;
  final VoidCallback onSelect;

  /// Wheel delta the iframe couldn't scroll itself — for the Flutter page around it.
  final void Function(double dy) onWheel;

  web.HTMLIFrameElement? _frame;
  web.Range? _kept;
  String _html = '';

  web.Document? get _doc => _frame?.contentDocument;

  Widget view(String html, String base) {
    _html = html;
    return HtmlElementView.fromTagName(
      tagName: 'iframe',
      onElementCreated: (e) {
        final f = _frame = (e as web.HTMLIFrameElement)
          ..setAttribute('sandbox', 'allow-same-origin')
          ..setAttribute('srcdoc', '${base.isEmpty ? '' : '<base href="$base/">'}<style>$_css</style>')
          ..style.border = '0'
          ..style.width = '100%'
          ..style.height = '100%';
        // Filled on every load: the browser reloads an iframe that gets re-attached.
        f.addEventListener(
          'load',
          (web.Event _) {
            final d = f.contentDocument!;
            d.designMode = 'on';
            d.execCommand('defaultParagraphSeparator', false, 'p');
            d.body!.innerHTML = (_html.trim().isEmpty ? '<p><br></p>' : _html).toJS;
            d.addEventListener('input', ((web.Event _) => _changed(d)).toJS);
            d.addEventListener('selectionchange', ((web.Event _) => onSelect()).toJS);
            d.addEventListener(
              'paste',
              (web.ClipboardEvent e) {
                final html = e.clipboardData?.getData('text/html') ?? '';
                if (html.isEmpty) return; // plain text: the browser's own paste is fine
                e.preventDefault();
                d.execCommand('insertHTML', false, cleanHtml(html));
              }.toJS,
            );
            d.addEventListener(
              'wheel',
              (web.WheelEvent e) {
                final s = d.scrollingElement!;
                final dy = e.deltaY * (e.deltaMode == 1 ? 40 : 1);
                if (dy < 0 ? s.scrollTop <= 0 : s.scrollTop + s.clientHeight >= s.scrollHeight - 1) onWheel(dy);
              }.toJS,
            );
          }.toJS,
        );
      },
    );
  }

  void _changed(web.Document d) => onChange(_html = (d.body!.innerHTML as JSString).toDart);

  /// Remembers the selection before a dialog / file picker takes the focus away.
  void keepSelection() {
    final s = _doc?.getSelection();
    _kept = s != null && s.rangeCount > 0 ? s.getRangeAt(0) : null;
  }

  /// Runs an editing command (bold, formatBlock, createLink…) on the kept or current selection.
  void exec(String command, [String value = '']) {
    final d = _doc;
    if (d == null) return;
    _frame!.contentWindow?.focus();
    if (_kept != null) {
      d.getSelection()!
        ..removeAllRanges()
        ..addRange(_kept!);
      _kept = null;
    }
    d.execCommand(command, false, value);
    _changed(d);
  }

  bool active(String command) => _doc?.queryCommandState(command) ?? false;

  /// Block tag at the caret: p, h2, h3, blockquote…
  String block() => _doc?.queryCommandValue('formatBlock') ?? '';
}

const _keepTags = {
  'p', 'h2', 'h3', 'h4', 'strong', 'b', 'em', 'i', 'u', 's', 'a', 'ul', 'ol', 'li', 'blockquote', 'img', 'br', 'hr', //
  'table', 'thead', 'tbody', 'tr', 'th', 'td', 'sub', 'sup',
};
const _dropTags = {'script', 'style', 'meta', 'link', 'title', 'iframe', 'object', 'embed', 'svg', 'template'};
const _keepAttrs = {'href', 'src', 'alt', 'colspan', 'rowspan'};

/// Pasted HTML (Word, Google Docs, other sites) → plain semantic tags without styles,
/// classes and wrappers, so it looks like the rest of the site. Google Docs keeps
/// bold/italic only in span styles and wraps everything in `<b id="docs-internal-…">`.
String cleanHtml(String html) {
  final doc = web.DOMParser().parseFromString(html.toJS, 'text/html');
  void walk(web.Element el) {
    for (var c = el.firstElementChild; c != null;) {
      final next = c.nextElementSibling, tag = c.tagName.toLowerCase();
      if (_dropTags.contains(tag)) {
        c.remove();
      } else {
        walk(c);
        if (_keepTags.contains(tag) && !c.id.startsWith('docs-internal')) {
          for (var i = c.attributes.length - 1; i >= 0; i--) {
            final a = c.attributes.item(i)!.name;
            if (!_keepAttrs.contains(a)) c.removeAttribute(a);
          }
        } else {
          // Unwrap; h1 → h2 (the page title is the only h1), styled spans → strong/em.
          final style = c.getAttribute('style') ?? '';
          final wrap = tag == 'h1'
              ? 'h2'
              : tag != 'span'
              ? null
              : RegExp(r'font-weight:\s*(bold|[6-9]00)').hasMatch(style)
              ? 'strong'
              : style.contains('italic')
              ? 'em'
              : null;
          final parent = c.parentNode!;
          final into = wrap == null ? parent : parent.insertBefore(doc.createElement(wrap), c);
          while (c.firstChild != null) {
            wrap == null ? parent.insertBefore(c.firstChild!, c) : into.appendChild(c.firstChild!);
          }
          c.remove();
        }
      }
      c = next;
    }
  }

  walk(doc.body!);
  return (doc.body!.innerHTML as JSString).toDart;
}

@JS('Intl.DisplayNames')
extension type _DisplayNames._(JSObject _) implements JSObject {
  external _DisplayNames(JSArray<JSString> locales, JSObject options);
  external String? of(String code);
}

final _regions = _DisplayNames(['uk'.toJS].toJS, {'type': 'region'}.jsify() as JSObject);

/// "CZ" → "Чехія" (the browser's own country names).
String countryName(String code) {
  try {
    return _regions.of(code) ?? code;
  } catch (_) {
    return code;
  }
}
