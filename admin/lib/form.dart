import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;

export 'api.dart' show J;

/// Thrown by a save callback when the user backed out of a confirmation.
class SaveCancelled implements Exception {
  const SaveCancelled();
}

enum T { section, text, slug, multiline, int, money, bool, date, select, html, image, images, files, options }

/// One editable field of a DTO map. Upsert requests take the same camelCase keys
/// as the admin DTOs, so the editor works on the raw JSON map and sends it back.
/// [T.section] starts a new card titled [label].
class F {
  const F(this.key, this.label, {this.t = T.text, this.required = false, this.folder, this.choices, this.hint});
  const F.section(this.label, {this.hint}) : key = '', t = T.section, required = false, folder = null, choices = null;
  final String key, label;
  final T t;
  final bool required;

  /// Helper text under the field (or under a section title).
  final String? hint;

  /// Media folder for uploads (html/image/images); null hides the image button.
  final String? folder;
  final Future<List<(Object?, String)>> Function()? choices;
}

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
    if (context.mounted) AppFeedback.showError(context, '$e');
    return null;
  }
}

Widget thumb(String? keyOrUrl, {double size = AppSizes.avatarLg}) => ClipRRect(
  borderRadius: AppRadius.allMd,
  child: keyOrUrl == null
      ? Builder(
          builder: (context) => Container(
            width: size,
            height: size,
            color: context.palette.chipBg,
            child: Icon(Icons.image_not_supported_outlined, size: size / 2.5, color: context.palette.textFaint),
          ),
        )
      : Image.network(
          mediaUrl(keyOrUrl),
          width: size,
          height: size,
          fit: BoxFit.cover,
          errorBuilder: (_, _, _) => Icon(Icons.broken_image_outlined, size: size / 2),
        ),
);

final _slug = RegExp(r'^[a-z0-9]+(?:-[a-z0-9]+)*$');
num? _money(String s) => num.tryParse(s.trim().replaceAll(' ', '').replaceAll(',', '.'));
num? _int(String s) => int.tryParse(s.trim());

/// Ukrainian latin transliteration for "suggest slug from the title".
String slugify(String s) {
  const m = {
    'а': 'a', 'б': 'b', 'в': 'v', 'г': 'h', 'ґ': 'g', 'д': 'd', 'е': 'e', 'є': 'ie', 'ж': 'zh', 'з': 'z', 'и': 'y',
    'і': 'i', 'ї': 'i', 'й': 'i', 'к': 'k', 'л': 'l', 'м': 'm', 'н': 'n', 'о': 'o', 'п': 'p', 'р': 'r', 'с': 's',
    'т': 't', 'у': 'u', 'ф': 'f', 'х': 'kh', 'ц': 'ts', 'ч': 'ch', 'ш': 'sh', 'щ': 'shch', 'ь': '', 'ю': 'iu', 'я': 'ia',
    "'": '', '’': '',
  };
  final latin = s.toLowerCase().split('').map((c) => m[c] ?? c).join();
  return latin.replaceAll(RegExp(r'[^a-z0-9]+'), '-').replaceAll(RegExp(r'^-+|-+$'), '');
}

Widget textField(
  J m,
  String key,
  String label, {
  bool required = false,
  int lines = 1,
  num? Function(String)? number,
  Object? empty,
  RegExp? pattern,
  String? hint,
  String? suffix,
  TextEditingController? controller,
}) => TextFormField(
  controller: controller,
  initialValue: controller == null ? (m[key]?.toString() ?? '') : null,
  minLines: 1,
  maxLines: lines,
  keyboardType: number == null ? null : const TextInputType.numberWithOptions(decimal: true),
  decoration: InputDecoration(labelText: required ? '$label *' : label, helperText: hint, suffixText: suffix),
  validator: (v) {
    v = v!.trim();
    if (v.isEmpty) return required ? "Обов'язкове поле" : null;
    if (number != null && number(v) == null) return 'Введіть число';
    if (pattern != null && !pattern.hasMatch(v)) return 'Лише a-z, 0-9 і дефіси, напр. vitryak-k5';
    return null;
  },
  onChanged: (v) => m[key] = number == null ? v : (v.trim().isEmpty ? empty : number(v)),
);

/// Ctrl/Cmd+S → [onSave] while this screen is the top route. A keyboard-level
/// handler, not Shortcuts: on the web focus often sits outside the page (or in a
/// DOM input), and the physical key keeps it working on the Ukrainian layout.
class SaveShortcut extends StatefulWidget {
  const SaveShortcut({super.key, required this.onSave, required this.child});
  final VoidCallback onSave;
  final Widget child;
  @override
  State<SaveShortcut> createState() => _SaveShortcutState();
}

class _SaveShortcutState extends State<SaveShortcut> {
  bool _onKey(KeyEvent e) {
    final kb = HardwareKeyboard.instance;
    if (e is! KeyDownEvent || e.physicalKey != PhysicalKeyboardKey.keyS || !(kb.isControlPressed || kb.isMetaPressed)) {
      return false;
    }
    if (!(ModalRoute.of(context)?.isCurrent ?? true)) return false;
    widget.onSave();
    return true;
  }

  @override
  void initState() {
    super.initState();
    HardwareKeyboard.instance.addHandler(_onKey);
  }

  @override
  void dispose() {
    HardwareKeyboard.instance.removeHandler(_onKey);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}

/// Full-page editor: fields grouped into kit section cards, Ctrl+S saves, leaving
/// with unsaved changes asks first, server errors show in a banner on top.
class EditorPage extends StatefulWidget {
  const EditorPage({
    super.key,
    required this.title,
    required this.fields,
    required this.data,
    required this.save,
    this.subtitle,
    this.actions = const [],
  });
  final String title;
  final String? subtitle;
  final List<F> fields;
  final J data;
  final Future<void> Function(J data) save;

  /// Extra app-bar actions (open on site, duplicate, delete…); each gets the page context.
  final List<Widget Function(BuildContext context)> actions;

  @override
  State<EditorPage> createState() => _EditorPageState();
}

class _EditorPageState extends State<EditorPage> with UnsavedChangesGuard {
  final _form = GlobalKey<FormState>();
  late String _initial = jsonEncode(widget.data);
  bool _busy = false;
  String? _error;

  @override
  bool get isDirty => jsonEncode(widget.data) != _initial;

  Future<void> _save() async {
    if (_busy) return;
    if (!_form.currentState!.validate()) {
      return AppFeedback.showError(context, 'Перевірте позначені поля');
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.save(widget.data);
      _initial = jsonEncode(widget.data);
      if (mounted) {
        AppFeedback.showSuccess(context, 'Збережено');
        Navigator.pop(context, true);
      }
    } on SaveCancelled {
      // the user chose not to — nothing to report
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  List<(F?, List<F>)> get _groups {
    final out = <(F?, List<F>)>[];
    for (final f in widget.fields) {
      if (f.t == T.section) {
        out.add((f, []));
      } else {
        if (out.isEmpty) out.add((null, []));
        out.last.$2.add(f);
      }
    }
    return out;
  }

  @override
  Widget build(BuildContext context) => guard(
    child: SaveShortcut(
      onSave: _save,
      child: Focus(
        autofocus: true,
        child: Scaffold(
          appBar: AppBar(
            leading: BackButton(onPressed: () async {
              if (await confirmClose() && context.mounted) Navigator.pop(context);
            }),
            title: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(widget.title),
                if (widget.subtitle != null) Text(widget.subtitle!, style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
            actions: [
              for (final a in widget.actions) a(context),
              AppSpacing.hGapSm,
              Tooltip(
                message: 'Ctrl+S',
                child: AppButton(label: 'Зберегти', icon: Icons.save_outlined, isLoading: _busy, onPressed: _save),
              ),
              AppSpacing.hGapLg,
            ],
          ),
          body: Form(
            key: _form,
            child: CenteredScrollColumn(
              maxWidth: AppSizes.dialogContentWidthXl,
              padding: AppSpacing.screenPadding,
              builder: (padding) => ListView(
                padding: padding,
                children: [
                  if (_error != null) ...[FormBanner(_error!), AppSpacing.gapLg],
                  for (final (head, fields) in _groups) ...[
                    AppSectionCard(
                      title: head?.label ?? 'Основне',
                      subtitle: head?.hint,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (final (i, f) in fields.indexed) ...[
                            if (i > 0) AppSpacing.gapLg,
                            FieldView(f, widget.data, onChanged: () => setState(() {})),
                          ],
                        ],
                      ),
                    ),
                    AppSpacing.gapLg,
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class FieldView extends StatefulWidget {
  const FieldView(this.f, this.data, {super.key, this.onChanged});
  final F f;
  final J data;
  final VoidCallback? onChanged;

  @override
  State<FieldView> createState() => _FieldViewState();
}

class _FieldViewState extends State<FieldView> {
  Future<List<(Object?, String)>>? _choices;

  F get f => widget.f;
  J get d => widget.data;
  List get list => (d[f.key] ??= <dynamic>[]) as List;

  void _set(VoidCallback fn) {
    setState(fn);
    widget.onChanged?.call();
  }

  /// List mutations renumber `sort` so order on screen = order on the site.
  void _mut(VoidCallback fn) => _set(() {
    fn();
    for (final (i, x) in list.indexed) {
      x['sort'] = i;
      for (final (j, v) in ((x['values'] as List?) ?? const []).indexed) {
        v['sort'] = j;
      }
    }
  });

  Widget _box(Widget child) => InputDecorator(
    decoration: InputDecoration(labelText: f.label, helperText: f.hint, border: const OutlineInputBorder()),
    child: child,
  );

  @override
  Widget build(BuildContext context) => switch (f.t) {
    T.section => const SizedBox.shrink(),
    T.text => textField(d, f.key, f.label, required: f.required, hint: f.hint),
    T.slug => _SlugField(f, d),
    T.multiline => textField(d, f.key, f.label, required: f.required, lines: 4, hint: f.hint),
    T.int => textField(d, f.key, f.label, number: _int, empty: 0, hint: f.hint),
    T.money => textField(d, f.key, f.label, required: f.required, number: _money, hint: f.hint, suffix: 'грн'),
    T.bool => SwitchListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(f.label),
      subtitle: f.hint == null ? null : Text(f.hint!),
      value: d[f.key] == true,
      onChanged: (v) => _set(() => d[f.key] = v),
    ),
    T.date => _box(
      InkWell(
        borderRadius: AppRadius.allMd,
        onTap: () async {
          final p = await showDatePicker(
            context: context,
            initialDate: DateTime.tryParse('${d[f.key]}')?.toLocal() ?? DateTime.now(),
            firstDate: DateTime(2015),
            lastDate: DateTime(2100),
          );
          if (p != null) _set(() => d[f.key] = DateTime(p.year, p.month, p.day, 12).toUtc().toIso8601String());
        },
        child: Row(
          children: [
            Expanded(child: Text(d[f.key] == null ? 'Сьогодні (за замовчуванням)' : fmtDate(d[f.key], time: false))),
            const Icon(Icons.calendar_month_outlined),
          ],
        ),
      ),
    ),
    T.select => FutureBuilder(
      future: _choices ??= f.choices!(),
      builder: (context, s) {
        if (s.hasError) return Text('${s.error}', style: TextStyle(color: Theme.of(context).colorScheme.error));
        if (!s.hasData) return const LinearProgressIndicator();
        final items = [if (!f.required) (null, '—'), ...s.data!];
        if (!items.any((x) => x.$1 == d[f.key])) d[f.key] = items.first.$1;
        return DropdownButtonFormField<Object?>(
          initialValue: d[f.key],
          decoration: InputDecoration(labelText: f.label, helperText: f.hint),
          items: [for (final x in items) DropdownMenuItem(value: x.$1, child: Text(x.$2))],
          onChanged: (v) => _set(() => d[f.key] = v),
        );
      },
    ),
    T.html => HtmlField(f, d),
    T.image => _box(
      Row(
        children: [
          thumb(d[f.key], size: 96),
          AppSpacing.hGapLg,
          OutlinedButton.icon(
            icon: const Icon(Icons.upload_outlined),
            label: Text(d[f.key] == null ? 'Завантажити' : 'Замінити'),
            onPressed: () async {
              final k = await uploadPicked(context, f.folder!, 'image/*');
              if (k != null) _set(() => d[f.key] = k);
            },
          ),
          if (d[f.key] != null)
            IconButton(
              tooltip: 'Прибрати',
              icon: const Icon(Icons.close),
              onPressed: () => _set(() => d[f.key] = null),
            ),
        ],
      ),
    ),
    T.images => _box(
      Wrap(
        spacing: AppSpacing.md,
        runSpacing: AppSpacing.md,
        children: [
          for (final (i, img) in list.indexed)
            SizedBox(
              key: ObjectKey(img),
              width: 180,
              child: Column(
                children: [
                  Stack(
                    children: [
                      thumb(img['key'], size: 180),
                      if (i == 0)
                        const Positioned(left: AppSpacing.sm, top: AppSpacing.sm, child: AppTag('Головне', icon: Icons.star)),
                    ],
                  ),
                  AppSpacing.gapXs,
                  textField(img, 'alt', 'Опис (alt)'),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      IconButton(
                        tooltip: 'Лівіше',
                        icon: const Icon(Icons.arrow_back),
                        onPressed: i == 0 ? null : () => _mut(() => list.insert(i - 1, list.removeAt(i))),
                      ),
                      IconButton(
                        tooltip: 'Правіше',
                        icon: const Icon(Icons.arrow_forward),
                        onPressed: i == list.length - 1 ? null : () => _mut(() => list.insert(i + 1, list.removeAt(i))),
                      ),
                      IconButton(
                        tooltip: 'Прибрати фото',
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () async {
                          if (await showConfirmDialog(context, message: 'Прибрати це фото з товару?', confirmLabel: 'Прибрати')) {
                            _mut(() => list.removeAt(i));
                          }
                        },
                      ),
                    ],
                  ),
                ],
              ),
            ),
          SizedBox(
            width: 180,
            height: 180,
            child: OutlinedButton.icon(
              icon: const Icon(Icons.add_photo_alternate_outlined),
              label: const Text('Додати фото'),
              onPressed: () async {
                final k = await uploadPicked(context, f.folder!, 'image/*');
                if (k != null) _mut(() => list.add({'key': k, 'alt': null}));
              },
            ),
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
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: Row(
                children: [
                  const Icon(Icons.description_outlined),
                  AppSpacing.hGapSm,
                  Expanded(child: textField(file, 'title', 'Назва файлу', required: true)),
                  IconButton(
                    tooltip: 'Відкрити',
                    icon: const Icon(Icons.open_in_new),
                    onPressed: () => browser.openTab(mediaUrl(file['key'])),
                  ),
                  IconButton(
                    tooltip: 'Прибрати',
                    icon: const Icon(Icons.delete_outline),
                    onPressed: () => _mut(() => list.removeAt(i)),
                  ),
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
              margin: const EdgeInsets.only(bottom: AppSpacing.md),
              child: Padding(
                padding: AppSpacing.cardPadding,
                child: Column(
                  children: [
                    Row(
                      children: [
                        Expanded(child: textField(o, 'nameUk', 'Опція (UK)', required: true)),
                        AppSpacing.hGapSm,
                        Expanded(child: textField(o, 'nameEn', 'Опція (EN)')),
                        IconButton(
                          tooltip: 'Видалити опцію',
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () async {
                            if (await showConfirmDialog(context,
                                message: 'Видалити опцію «${o['nameUk'] ?? ''}» з усіма значеннями?')) {
                              _mut(() => list.removeAt(i));
                            }
                          },
                        ),
                      ],
                    ),
                    for (final (j, v) in (o['values'] as List).indexed)
                      Padding(
                        key: ObjectKey(v),
                        padding: const EdgeInsets.only(top: AppSpacing.sm, left: AppSpacing.xl),
                        child: Row(
                          children: [
                            Expanded(child: textField(v, 'valueUk', 'Значення (UK)', required: true)),
                            AppSpacing.hGapSm,
                            Expanded(child: textField(v, 'valueEn', 'Значення (EN)')),
                            AppSpacing.hGapSm,
                            SizedBox(
                              width: 140,
                              child: textField(v, 'priceDelta', '± до ціни', number: _money, empty: 0, suffix: 'грн'),
                            ),
                            IconButton(
                              tooltip: 'Прибрати значення',
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

/// Slug with a "from the title" helper (transliterated) and the live URL preview.
class _SlugField extends StatefulWidget {
  const _SlugField(this.f, this.d);
  final F f;
  final J d;
  @override
  State<_SlugField> createState() => _SlugFieldState();
}

class _SlugFieldState extends State<_SlugField> {
  late final _c = TextEditingController(text: widget.d[widget.f.key] ?? '');

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final title = (widget.d['nameUk'] ?? widget.d['titleUk'] ?? '') as String;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: textField(widget.d, widget.f.key, widget.f.label,
              required: widget.f.required, pattern: _slug, hint: widget.f.hint, controller: _c),
        ),
        AppSpacing.hGapSm,
        Padding(
          padding: const EdgeInsets.only(top: AppSpacing.sm),
          child: IconButton(
            tooltip: 'Згенерувати з назви',
            icon: const Icon(Icons.auto_fix_high_outlined),
            onPressed: title.trim().isEmpty
                ? null
                : () {
                    _c.text = slugify(title);
                    widget.d[widget.f.key] = _c.text;
                  },
          ),
        ),
      ],
    );
  }
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
    Widget tag(String label, String tip, String open, String close) =>
        Tooltip(message: tip, child: TextButton(onPressed: () => _wrap(open, close), child: Text(label)));
    return InputDecorator(
      decoration: InputDecoration(
        labelText: f.required ? '${f.label} *' : f.label,
        helperText: f.hint,
        border: const OutlineInputBorder(),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              tag('B', 'Жирний', '<strong>', '</strong>'),
              tag('I', 'Курсив', '<em>', '</em>'),
              tag('H2', 'Заголовок 2', '<h2>', '</h2>'),
              tag('H3', 'Заголовок 3', '<h3>', '</h3>'),
              tag('¶', 'Абзац', '<p>', '</p>'),
              tag('• список', 'Маркований список', '<ul>\n<li>', '</li>\n</ul>'),
              tag('li', 'Пункт списку', '<li>', '</li>'),
              tag('посилання', 'Посилання', '<a href="">', '</a>'),
              if (f.folder != null)
                TextButton.icon(
                  icon: const Icon(Icons.image_outlined, size: AppSizes.iconSm),
                  label: const Text('фото'),
                  onPressed: () async {
                    final k = await uploadPicked(context, f.folder!, 'image/*');
                    if (k != null) _wrap('<img src="/media/$k" alt="">');
                  },
                ),
              AppSpacing.hGapLg,
              FilterChip(
                avatar: const Icon(Icons.visibility_outlined, size: AppSizes.iconSm),
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
              decoration: const InputDecoration(border: InputBorder.none, filled: false),
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

/// "12 345,50 грн" (thin-space thousands, comma decimals, no ",00").
String money(Object? v) {
  final n = (v as num?) ?? 0;
  final whole = n.truncate().abs().toString().replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ' ');
  final cents = ((n.abs() - n.abs().truncate()) * 100).round();
  return '${n < 0 ? '−' : ''}$whole${cents == 0 ? '' : ',${cents.toString().padLeft(2, '0')}'} грн';
}
