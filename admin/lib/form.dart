import 'package:flutter/material.dart';

import 'api.dart';
import 'browser.dart' as browser;

typedef J = Map<String, dynamic>;

enum T { text, slug, multiline, int, money, bool, date, select, html, image, images, files, options }

/// One editable field of a DTO map. Upsert requests take the same camelCase keys
/// as the admin DTOs, so the editor works on the raw JSON map and sends it back.
class F {
  const F(this.key, this.label, {this.t = T.text, this.required = false, this.folder, this.choices});
  final String key, label;
  final T t;
  final bool required;

  /// Media folder for uploads (html/image/images); null hides the image button.
  final String? folder;
  final Future<List<(Object?, String)>> Function()? choices;
}

void toast(BuildContext context, String text) =>
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));

Future<bool> confirm(BuildContext context, String text) async =>
    await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        content: Text(text),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Скасувати')),
          FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Так')),
        ],
      ),
    ) ==
    true;

/// Picks a file, uploads it to [folder]; returns the media key.
Future<String?> uploadPicked(
  BuildContext context,
  String folder,
  String accept, [
  void Function(String name)? onName,
]) async {
  final f = await browser.pickFile(accept);
  if (f == null) return null;
  try {
    final key = await api.upload(folder, f);
    onName?.call(f.name);
    return key;
  } catch (e) {
    if (context.mounted) toast(context, '$e');
    return null;
  }
}

Widget thumb(String? keyOrUrl, {double size = 48}) => keyOrUrl == null
    ? SizedBox(width: size, height: size)
    : Image.network(
        mediaUrl(keyOrUrl),
        width: size,
        height: size,
        fit: BoxFit.cover,
        errorBuilder: (_, _, _) => Icon(Icons.broken_image, size: size),
      );

final _slug = RegExp(r'^[a-z0-9]+(?:-[a-z0-9]+)*$');
num? _money(String s) => num.tryParse(s.trim().replaceAll(',', '.'));
num? _int(String s) => int.tryParse(s.trim());

Widget textField(
  J m,
  String key,
  String label, {
  bool required = false,
  int lines = 1,
  num? Function(String)? number,
  Object? empty,
  RegExp? pattern,
}) => TextFormField(
  initialValue: m[key]?.toString() ?? '',
  minLines: 1,
  maxLines: lines,
  decoration: InputDecoration(labelText: required ? '$label *' : label, border: const OutlineInputBorder()),
  validator: (v) {
    v = v!.trim();
    if (v.isEmpty) return required ? "Обов'язкове поле" : null;
    if (number != null && number(v) == null) return 'Введіть число';
    if (pattern != null && !pattern.hasMatch(v)) return 'Лише a-z, 0-9 і дефіси';
    return null;
  },
  onChanged: (v) => m[key] = number == null ? v : (v.trim().isEmpty ? empty : number(v)),
);

class EditorPage extends StatefulWidget {
  const EditorPage({super.key, required this.title, required this.fields, required this.data, required this.save});
  final String title;
  final List<F> fields;
  final J data;
  final Future<void> Function(J data) save;

  @override
  State<EditorPage> createState() => _EditorPageState();
}

class _EditorPageState extends State<EditorPage> {
  final _form = GlobalKey<FormState>();
  bool _busy = false;

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return toast(context, 'Перевірте поля');
    setState(() => _busy = true);
    try {
      await widget.save(widget.data);
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) toast(context, '$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(widget.title),
      actions: [
        Padding(
          padding: const EdgeInsets.only(right: 16),
          child: FilledButton.icon(
            onPressed: _busy ? null : _save,
            icon: const Icon(Icons.save),
            label: const Text('Зберегти'),
          ),
        ),
      ],
    ),
    body: Form(
      key: _form,
      child: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 1000),
              child: Column(
                children: [
                  for (final f in widget.fields)
                    Padding(padding: const EdgeInsets.only(bottom: 16), child: FieldView(f, widget.data)),
                ],
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class FieldView extends StatefulWidget {
  const FieldView(this.f, this.data, {super.key});
  final F f;
  final J data;

  @override
  State<FieldView> createState() => _FieldViewState();
}

class _FieldViewState extends State<FieldView> {
  Future<List<(Object?, String)>>? _choices;

  F get f => widget.f;
  J get d => widget.data;
  List get list => (d[f.key] ??= <dynamic>[]) as List;

  /// List mutations renumber `sort` so order on screen = order on the site.
  void _mut(VoidCallback fn) => setState(() {
    fn();
    for (final (i, x) in list.indexed) {
      x['sort'] = i;
      for (final (j, v) in ((x['values'] as List?) ?? const []).indexed) {
        v['sort'] = j;
      }
    }
  });

  Widget _box(Widget child) => InputDecorator(
    decoration: InputDecoration(labelText: f.label, border: const OutlineInputBorder()),
    child: child,
  );

  @override
  Widget build(BuildContext context) => switch (f.t) {
    T.text => textField(d, f.key, f.label, required: f.required),
    T.slug => textField(d, f.key, f.label, required: f.required, pattern: _slug),
    T.multiline => textField(d, f.key, f.label, required: f.required, lines: 4),
    T.int => textField(d, f.key, f.label, number: _int, empty: 0),
    T.money => textField(d, f.key, f.label, required: f.required, number: _money),
    T.bool => SwitchListTile(
      title: Text(f.label),
      value: d[f.key] == true,
      onChanged: (v) => setState(() => d[f.key] = v),
    ),
    T.date => ListTile(
      title: Text(f.label),
      subtitle: Text(fmtDate(d[f.key], time: false)),
      trailing: const Icon(Icons.calendar_month),
      onTap: () async {
        final p = await showDatePicker(
          context: context,
          initialDate: DateTime.tryParse('${d[f.key]}')?.toLocal() ?? DateTime.now(),
          firstDate: DateTime(2015),
          lastDate: DateTime(2100),
        );
        if (p != null) setState(() => d[f.key] = DateTime(p.year, p.month, p.day, 12).toUtc().toIso8601String());
      },
    ),
    T.select => FutureBuilder(
      future: _choices ??= f.choices!(),
      builder: (context, s) {
        if (!s.hasData) return const LinearProgressIndicator();
        final items = [if (!f.required) (null, '—'), ...s.data!];
        if (!items.any((x) => x.$1 == d[f.key])) d[f.key] = items.first.$1;
        return DropdownButtonFormField<Object?>(
          initialValue: d[f.key],
          decoration: InputDecoration(labelText: f.label, border: const OutlineInputBorder()),
          items: [for (final x in items) DropdownMenuItem(value: x.$1, child: Text(x.$2))],
          onChanged: (v) => d[f.key] = v,
        );
      },
    ),
    T.html => HtmlField(f, d),
    T.image => _box(
      Row(
        children: [
          thumb(d[f.key], size: 96),
          const SizedBox(width: 16),
          TextButton.icon(
            icon: const Icon(Icons.upload),
            label: const Text('Завантажити'),
            onPressed: () async {
              final k = await uploadPicked(context, f.folder!, 'image/*');
              if (k != null) setState(() => d[f.key] = k);
            },
          ),
          if (d[f.key] != null)
            IconButton(
              tooltip: 'Прибрати',
              icon: const Icon(Icons.close),
              onPressed: () => setState(() => d[f.key] = null),
            ),
        ],
      ),
    ),
    T.images => _box(
      Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          for (final (i, img) in list.indexed)
            SizedBox(
              key: ObjectKey(img),
              width: 180,
              child: Column(
                children: [
                  thumb(img['key'], size: 180),
                  const SizedBox(height: 4),
                  textField(img, 'alt', 'alt'),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      IconButton(
                        icon: const Icon(Icons.arrow_back),
                        onPressed: i == 0 ? null : () => _mut(() => list.insert(i - 1, list.removeAt(i))),
                      ),
                      IconButton(
                        icon: const Icon(Icons.arrow_forward),
                        onPressed: i == list.length - 1 ? null : () => _mut(() => list.insert(i + 1, list.removeAt(i))),
                      ),
                      IconButton(icon: const Icon(Icons.delete_outline), onPressed: () => _mut(() => list.removeAt(i))),
                    ],
                  ),
                ],
              ),
            ),
          OutlinedButton.icon(
            icon: const Icon(Icons.add_photo_alternate),
            label: const Text('Додати фото'),
            onPressed: () async {
              final k = await uploadPicked(context, f.folder!, 'image/*');
              if (k != null) _mut(() => list.add({'key': k, 'alt': null}));
            },
          ),
        ],
      ),
    ),
    T.files => _box(
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final (i, file) in list.indexed)
            Padding(
              key: ObjectKey(file),
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Expanded(child: textField(file, 'title', 'Назва файлу', required: true)),
                  IconButton(icon: const Icon(Icons.delete_outline), onPressed: () => _mut(() => list.removeAt(i))),
                ],
              ),
            ),
          OutlinedButton.icon(
            icon: const Icon(Icons.attach_file),
            label: const Text('Додати файл (PDF/Word)'),
            onPressed: () async {
              String? name;
              final k = await uploadPicked(context, 'files', '.pdf,.doc,.docx', (n) => name = n);
              if (k != null) _mut(() => list.add({'key': k, 'title': name!.replaceAll(RegExp(r'\.\w+$'), '')}));
            },
          ),
        ],
      ),
    ),
    T.options => _box(
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final (i, o) in list.indexed)
            Card(
              key: ObjectKey(o),
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  children: [
                    Row(
                      children: [
                        Expanded(child: textField(o, 'nameUk', 'Опція (UK)', required: true)),
                        const SizedBox(width: 8),
                        Expanded(child: textField(o, 'nameEn', 'Опція (EN)')),
                        IconButton(
                          tooltip: 'Видалити опцію',
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () => _mut(() => list.removeAt(i)),
                        ),
                      ],
                    ),
                    for (final (j, v) in (o['values'] as List).indexed)
                      Padding(
                        key: ObjectKey(v),
                        padding: const EdgeInsets.only(top: 8, left: 24),
                        child: Row(
                          children: [
                            Expanded(child: textField(v, 'valueUk', 'Значення (UK)', required: true)),
                            const SizedBox(width: 8),
                            Expanded(child: textField(v, 'valueEn', 'Значення (EN)')),
                            const SizedBox(width: 8),
                            SizedBox(
                              width: 140,
                              child: textField(v, 'priceDelta', '± до ціни', number: _money, empty: 0),
                            ),
                            IconButton(
                              icon: const Icon(Icons.close),
                              onPressed: () => _mut(() => (o['values'] as List).removeAt(j)),
                            ),
                          ],
                        ),
                      ),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        icon: const Icon(Icons.add),
                        label: const Text('Значення'),
                        onPressed: () => _mut(() => (o['values'] as List).add({'valueUk': '', 'priceDelta': 0})),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          OutlinedButton.icon(
            icon: const Icon(Icons.add),
            label: const Text('Додати опцію'),
            onPressed: () => _mut(() => list.add({'nameUk': '', 'values': []})),
          ),
        ],
      ),
    ),
  };
}

/// Raw HTML (what Weblium exported and what Nuxt renders) with a tag toolbar and a
/// sandboxed preview. ponytail: no WYSIWYG — add one if editors find tags painful.
class HtmlField extends StatefulWidget {
  const HtmlField(this.f, this.data, {super.key});
  final F f;
  final J data;

  @override
  State<HtmlField> createState() => _HtmlFieldState();
}

class _HtmlFieldState extends State<HtmlField> {
  late final TextEditingController _c = TextEditingController(text: widget.data[widget.f.key] ?? '')
    ..addListener(() => widget.data[widget.f.key] = _c.text);
  bool _preview = false;

  void _wrap(String open, [String close = '']) {
    final t = _c.text;
    final s = _c.selection.isValid ? _c.selection : TextSelection.collapsed(offset: t.length);
    final inner = s.textInside(t);
    _c.value = TextEditingValue(
      text: t.replaceRange(s.start, s.end, '$open$inner$close'),
      selection: TextSelection.collapsed(offset: s.start + open.length + inner.length + close.length),
    );
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final f = widget.f;
    Widget tag(String label, String open, String close) =>
        TextButton(onPressed: () => _wrap(open, close), child: Text(label));
    return InputDecorator(
      decoration: InputDecoration(labelText: f.required ? '${f.label} *' : f.label, border: const OutlineInputBorder()),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              tag('B', '<strong>', '</strong>'),
              tag('I', '<em>', '</em>'),
              tag('H2', '<h2>', '</h2>'),
              tag('H3', '<h3>', '</h3>'),
              tag('¶', '<p>', '</p>'),
              tag('• список', '<ul>\n<li>', '</li>\n</ul>'),
              tag('li', '<li>', '</li>'),
              tag('посилання', '<a href="">', '</a>'),
              if (f.folder != null)
                TextButton.icon(
                  icon: const Icon(Icons.image, size: 18),
                  label: const Text('фото'),
                  onPressed: () async {
                    final k = await uploadPicked(context, f.folder!, 'image/*');
                    if (k != null) _wrap('<img src="/media/$k" alt="">');
                  },
                ),
              const SizedBox(width: 16),
              FilterChip(
                label: const Text('Перегляд'),
                selected: _preview,
                onSelected: (v) => setState(() => _preview = v),
              ),
            ],
          ),
          if (_preview) SizedBox(height: 420, child: browser.htmlPreview(_c.text, apiHost)),
          Offstage(
            offstage: _preview,
            child: TextFormField(
              controller: _c,
              minLines: 6,
              maxLines: 20,
              style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
              validator: (v) => f.required && v!.trim().isEmpty ? "Обов'язкове поле" : null,
            ),
          ),
        ],
      ),
    );
  }
}

String fmtDate(Object? iso, {bool time = true}) {
  final d = DateTime.tryParse('${iso ?? ''}')?.toLocal();
  if (d == null) return '—';
  String two(int n) => n.toString().padLeft(2, '0');
  return '${two(d.day)}.${two(d.month)}.${d.year}${time ? ' ${two(d.hour)}:${two(d.minute)}' : ''}';
}
