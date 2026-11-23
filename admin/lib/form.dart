import 'package:flutter/material.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'slug.dart';

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

/// Replaces the current snackbar: with accessibility on, snackbars never time out and would queue
/// a later error behind an old "Збережено".
void toast(BuildContext context, String text) => ScaffoldMessenger.of(context)
  ..hideCurrentSnackBar()
  ..showSnackBar(SnackBar(content: Text(text)));

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
    T.slug => textField(d, f.key, f.label, required: f.required, pattern: slugPattern),
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

/// Text of an HTML fragment (card excerpts, empty-body checks).
String plainText(Object? html) => '${html ?? ''}'
    .replaceAll(RegExp(r'<[^>]*>'), ' ')
    .replaceAll('&nbsp;', ' ')
    .replaceAll('&amp;', '&')
    .replaceAll('&quot;', '"')
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll(RegExp(r'\s+'), ' ')
    .trim();

bool blankHtml(Object? html) => plainText(html).isEmpty && !'${html ?? ''}'.contains('<img');

/// One-line text prompt; null when cancelled.
Future<String?> ask(BuildContext context, String title, String hint) {
  final c = TextEditingController();
  return showDialog<String>(
    context: context,
    builder: (d) => AlertDialog(
      title: Text(title),
      content: SizedBox(
        width: 420,
        child: TextField(
          controller: c,
          autofocus: true,
          decoration: InputDecoration(hintText: hint, border: const OutlineInputBorder()),
          onSubmitted: (v) => Navigator.pop(d, v),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(d), child: const Text('Скасувати')),
        FilledButton(onPressed: () => Navigator.pop(d, c.text), child: const Text('OK')),
      ],
    ),
  );
}

const _blocks = {'p': 'Абзац', 'h2': 'Заголовок', 'h3': 'Підзаголовок', 'blockquote': 'Цитата'};

/// HTML field (what Weblium exported and what Nuxt renders): a visual editor with a
/// toolbar, plus a source view for raw tags.
class HtmlField extends StatefulWidget {
  const HtmlField(this.f, this.data, {super.key, this.height = 420});
  final F f;
  final J data;

  /// Editor height; null fills the parent (the blog post dialog).
  final double? height;

  @override
  State<HtmlField> createState() => _HtmlFieldState();
}

class _HtmlFieldState extends State<HtmlField> {
  late final _doc = browser.RichDoc(
    onChange: (h) => widget.data[widget.f.key] = h,
    onSelect: () => mounted ? setState(() {}) : null, // toolbar shows bold/heading at the caret
    onWheel: (dy) {
      final p = Scrollable.maybeOf(context, axis: Axis.vertical)?.position;
      p?.jumpTo((p.pixels + dy).clamp(p.minScrollExtent, p.maxScrollExtent));
    },
  );
  TextEditingController? _source; // non-null = raw HTML view

  String get _html => widget.data[widget.f.key] as String? ?? '';

  void _toggleSource() => setState(() {
    if (_source == null) {
      final c = TextEditingController(text: _html);
      _source = c..addListener(() => widget.data[widget.f.key] = c.text);
    } else {
      _source!.dispose();
      _source = null;
    }
  });

  Future<void> _link() async {
    _doc.keepSelection();
    final url = (await ask(context, 'Посилання', 'https://…'))?.trim() ?? '';
    if (url.isNotEmpty) _doc.exec('createLink', url);
  }

  Future<void> _image() async {
    _doc.keepSelection();
    final k = await uploadPicked(context, widget.f.folder!, 'image/*');
    if (k != null) _doc.exec('insertHTML', '<img src="/media/$k" alt="">');
  }

  @override
  void dispose() {
    _source?.dispose();
    super.dispose();
  }

  Widget _toolbar() {
    final visual = _source == null;
    Widget btn(IconData icon, String tip, VoidCallback on, [bool selected = false]) => IconButton(
      icon: Icon(icon, size: 20),
      tooltip: tip,
      isSelected: selected,
      visualDensity: VisualDensity.compact,
      onPressed: visual ? on : null,
    );
    Widget cmd(IconData icon, String tip, String command) =>
        btn(icon, tip, () => _doc.exec(command), visual && _doc.active(command));
    const gap = SizedBox(height: 24, child: VerticalDivider(width: 16));
    final block = _doc.block();
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          DropdownButton<String>(
            value: _blocks.containsKey(block) ? block : 'p',
            underline: const SizedBox.shrink(),
            focusColor: Colors.transparent,
            items: [for (final e in _blocks.entries) DropdownMenuItem(value: e.key, child: Text(e.value))],
            onChanged: visual ? (v) => _doc.exec('formatBlock', '<$v>') : null,
          ),
          gap,
          cmd(Icons.format_bold, 'Жирний (Ctrl+B)', 'bold'),
          cmd(Icons.format_italic, 'Курсив (Ctrl+I)', 'italic'),
          cmd(Icons.format_underlined, 'Підкреслений (Ctrl+U)', 'underline'),
          cmd(Icons.format_strikethrough, 'Закреслений', 'strikeThrough'),
          gap,
          cmd(Icons.format_list_bulleted, 'Список', 'insertUnorderedList'),
          cmd(Icons.format_list_numbered, 'Нумерований список', 'insertOrderedList'),
          btn(Icons.link, 'Посилання', _link),
          btn(Icons.link_off, 'Прибрати посилання', () => _doc.exec('unlink')),
          if (widget.f.folder != null) btn(Icons.image_outlined, 'Вставити фото', _image),
          btn(Icons.format_clear, 'Очистити форматування', () => _doc.exec('removeFormat')),
          gap,
          btn(Icons.undo, 'Скасувати (Ctrl+Z)', () => _doc.exec('undo')),
          btn(Icons.redo, 'Повторити (Ctrl+Y)', () => _doc.exec('redo')),
          gap,
          IconButton(
            icon: const Icon(Icons.code, size: 20),
            tooltip: visual ? 'Редагувати HTML' : 'Візуальний редактор',
            isSelected: !visual,
            visualDensity: VisualDensity.compact,
            onPressed: _toggleSource,
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final f = widget.f, cs = Theme.of(context).colorScheme, fill = widget.height == null;
    final body = _source == null
        ? _doc.view(_html, apiHost)
        : TextField(
            controller: _source,
            expands: true,
            maxLines: null,
            style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
            decoration: const InputDecoration(border: InputBorder.none, contentPadding: EdgeInsets.all(16)),
          );
    return FormField<void>(
      validator: (_) => f.required && blankHtml(_html) ? "Обов'язкове поле" : null,
      builder: (state) {
        final box = DecoratedBox(
          decoration: BoxDecoration(
            color: cs.surface,
            border: Border.all(color: state.hasError ? cs.error : cs.outline),
            borderRadius: BorderRadius.circular(4),
          ),
          child: Column(
            children: [
              _toolbar(),
              const Divider(height: 1),
              fill ? Expanded(child: body) : SizedBox(height: widget.height, child: body),
            ],
          ),
        );
        return Column(
          mainAxisSize: fill ? MainAxisSize.max : MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(left: 4, bottom: 6),
              child: Text(f.required ? '${f.label} *' : f.label, style: TextStyle(color: cs.onSurfaceVariant)),
            ),
            fill ? Expanded(child: box) : box,
            if (state.hasError)
              Padding(
                padding: const EdgeInsets.only(left: 12, top: 6),
                child: Text(state.errorText!, style: TextStyle(color: cs.error, fontSize: 12)),
              ),
          ],
        );
      },
    );
  }
}

String fmtDate(Object? iso, {bool time = true}) {
  final d = DateTime.tryParse('${iso ?? ''}')?.toLocal();
  if (d == null) return '—';
  String two(int n) => n.toString().padLeft(2, '0');
  return '${two(d.day)}.${two(d.month)}.${d.year}${time ? ' ${two(d.hour)}:${two(d.minute)}' : ''}';
}
