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

/// Renders untrusted HTML in a sandboxed iframe with scripts off, so a post body
/// can't touch the admin's session (allow-same-origin alone is harmless without
/// allow-scripts; a fully opaque origin blocked the images). [base] (API origin,
/// empty = this origin, which srcdoc inherits) resolves `/media/...` links.
Widget htmlPreview(String html, String base) => HtmlElementView.fromTagName(
  key: ValueKey(html),
  tagName: 'iframe',
  onElementCreated: (e) => (e as web.HTMLIFrameElement)
    ..setAttribute('sandbox', 'allow-same-origin')
    ..setAttribute(
      'srcdoc',
      '${base.isEmpty ? '' : '<base href="$base/">'}<style>body{font-family:sans-serif}img{max-width:100%}</style>$html',
    )
    ..style.border = '0'
    ..style.width = '100%'
    ..style.height = '100%',
);
