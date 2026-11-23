import 'dart:convert';

import 'package:flutter/material.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';
import 'slug.dart';

// Blog as it was in Weblium: blog settings (author), Published / Drafts tabs with post
// cards, and a dialog editor with Content and SEO tabs.

const _path = '/admin/blog';
const _images = 'image/png,image/jpeg,image/webp';

/// Public site for "view on site": admin.uagreen.co → uagreen.co,
/// admin-new.uagreen.co → new.uagreen.co, local dev → Nuxt on :3000.
String get _site {
  final u = Uri.base;
  return u.host == 'localhost'
      ? 'http://localhost:3000'
      : '${u.scheme}://${u.host.replaceFirst(RegExp(r'^admin[.-]'), '')}';
}

/// Published with a future date: the site shows it from that day on.
bool _scheduled(J p) =>
    p['published'] == true && (DateTime.tryParse('${p['publishedAt']}')?.isAfter(DateTime.now()) ?? false);

class BlogPage extends StatefulWidget {
  const BlogPage({super.key});
  @override
  State<BlogPage> createState() => _BlogPageState();
}

enum _Act { view, duplicate, publish, delete }

class _BlogPageState extends State<BlogPage> {
  late Future<List<J>> _posts = _load();
  bool _drafts = false;
  String _q = '';

  Future<List<J>> _load() async => (await api.get(_path) as List).cast<J>();

  Future<void> _run(Future<bool?> Function() fn) async {
    try {
      if (await fn() == true) setState(() => _posts = _load());
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
  }

  void _edit(J? post, [int tab = 0]) => _run(
    () => showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => PostEditor(post: post, tab: tab),
    ),
  );

  void _act(_Act a, J p, List<J> all) => _run(() async {
    switch (a) {
      case _Act.view:
        browser.openUrl('$_site/blog/${p['slug']}');
        return false;
      case _Act.duplicate:
        final taken = {for (final x in all) x['slug']};
        var slug = '${p['slug']}-copy';
        for (var i = 2; taken.contains(slug); i++) {
          slug = '${p['slug']}-copy-$i';
        }
        await api.post(_path, {...p, 'slug': slug, 'titleUk': '${p['titleUk']} (копія)', 'published': false});
        if (mounted) toast(context, 'Копію збережено в чернетках');
      case _Act.publish:
        await api.put('$_path/${p['id']}', {...p, 'published': p['published'] != true});
      case _Act.delete:
        if (!await confirm(context, 'Видалити допис «${p['titleUk']}» назавжди?')) return false;
        await api.delete('$_path/${p['id']}');
    }
    return true;
  });

  @override
  Widget build(BuildContext context) => FutureBuilder(
    future: _posts,
    builder: (context, s) {
      final all = s.data ?? const <J>[];
      final published = all.where((p) => p['published'] == true).toList();
      final drafts = all.where((p) => p['published'] != true).toList();
      final q = _q.toLowerCase();
      final shown = (_drafts ? drafts : published)
          .where((p) => q.isEmpty || '${p['titleUk']} ${p['titleEn'] ?? ''}'.toLowerCase().contains(q))
          .toList();
      final cs = Theme.of(context).colorScheme;
      final tabs = DefaultTabController(
        length: 2,
        initialIndex: _drafts ? 1 : 0,
        child: TabBar(
          isScrollable: true,
          tabAlignment: TabAlignment.start,
          dividerColor: Colors.transparent,
          labelStyle: Theme.of(context).textTheme.titleMedium,
          onTap: (i) => setState(() => _drafts = i == 1),
          tabs: [
            Tab(text: 'Опубліковані (${published.length})'),
            Tab(text: 'Чернетки (${drafts.length})'),
          ],
        ),
      );
      final search = TextField(
        decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Пошук', isDense: true),
        onChanged: (v) => setState(() => _q = v),
      );
      final create = FilledButton.icon(
        onPressed: () => _edit(null),
        icon: const Icon(Icons.add),
        label: const Text('Створити допис'),
      );
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 1100),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const _BlogSettings(),
                  const SizedBox(height: 24),
                  LayoutBuilder(
                    builder: (context, c) => c.maxWidth >= 720
                        ? Row(
                            children: [
                              Expanded(child: tabs),
                              SizedBox(width: 220, child: search),
                              const SizedBox(width: 16),
                              create,
                            ],
                          )
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              tabs,
                              const SizedBox(height: 8),
                              Row(
                                children: [
                                  Expanded(child: search),
                                  const SizedBox(width: 12),
                                  create,
                                ],
                              ),
                            ],
                          ),
                  ),
                  const Divider(height: 1),
                  const SizedBox(height: 20),
                  if (s.hasError)
                    Center(child: Text('${s.error}'))
                  else if (!s.hasData)
                    const Center(child: CircularProgressIndicator())
                  else if (shown.isEmpty)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 48),
                      child: Column(
                        children: [
                          Icon(Icons.article_outlined, size: 48, color: cs.outline),
                          const SizedBox(height: 12),
                          Text(
                            q.isNotEmpty
                                ? 'Нічого не знайдено'
                                : _drafts
                                ? 'Чернеток немає'
                                : 'Ще немає опублікованих дописів',
                          ),
                        ],
                      ),
                    )
                  else
                    for (final p in shown)
                      _PostCard(p, onEdit: () => _edit(p), onSeo: () => _edit(p, 1), onAct: (a) => _act(a, p, all)),
                ],
              ),
            ),
          ),
        ],
      );
    },
  );
}

class _PostCard extends StatelessWidget {
  const _PostCard(this.p, {required this.onEdit, required this.onSeo, required this.onAct});
  final J p;
  final VoidCallback onEdit, onSeo;
  final void Function(_Act) onAct;

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme, cs = Theme.of(context).colorScheme;
    final published = p['published'] == true;
    final blank = ColoredBox(
      color: cs.surfaceContainerHighest,
      child: Center(child: Icon(Icons.image_outlined, size: 40, color: cs.outline)),
    );
    final cover = p['coverUrl'] == null
        ? blank
        : Image.network(mediaUrl(p['coverUrl']), fit: BoxFit.cover, errorBuilder: (_, _, _) => blank);
    Widget tag(String text, [IconData? icon]) => Chip(
      avatar: icon == null ? null : Icon(icon, size: 16),
      label: Text(text),
      visualDensity: VisualDensity.compact,
      labelStyle: t.labelSmall,
    );
    final info = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '${p['titleUk']}',
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: t.titleMedium?.copyWith(fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 8),
        Text(
          (p['excerptUk'] as String?) ?? plainText(p['bodyHtmlUk']),
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: t.bodyMedium?.copyWith(color: cs.onSurfaceVariant),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text(fmtDate(p['publishedAt'], time: false), style: t.bodySmall?.copyWith(color: cs.outline)),
            if (_scheduled(p)) tag('Заплановано', Icons.schedule),
            if (p['titleEn'] != null) tag('EN'),
            if (p['noIndex'] == true) tag('Прихований від пошуку', Icons.visibility_off_outlined),
          ],
        ),
      ],
    );
    final actions = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        OutlinedButton(onPressed: onEdit, child: const Text('Редагувати')),
        const SizedBox(width: 8),
        IconButton.outlined(tooltip: 'Налаштування й SEO', onPressed: onSeo, icon: const Icon(Icons.settings_outlined)),
        PopupMenuButton<_Act>(
          tooltip: 'Ще',
          icon: const Icon(Icons.more_horiz),
          onSelected: onAct,
          itemBuilder: (_) => [
            if (published && !_scheduled(p))
              const PopupMenuItem(
                value: _Act.view,
                child: ListTile(leading: Icon(Icons.open_in_new), title: Text('Переглянути на сайті')),
              ),
            const PopupMenuItem(
              value: _Act.duplicate,
              child: ListTile(leading: Icon(Icons.copy_outlined), title: Text('Дублювати')),
            ),
            PopupMenuItem(
              value: _Act.publish,
              child: ListTile(
                leading: Icon(published ? Icons.unpublished_outlined : Icons.publish),
                title: Text(published ? 'Перенести в чернетки' : 'Опублікувати'),
              ),
            ),
            const PopupMenuDivider(),
            PopupMenuItem(
              value: _Act.delete,
              child: ListTile(
                leading: Icon(Icons.delete_outline, color: cs.error),
                title: Text('Видалити', style: TextStyle(color: cs.error)),
              ),
            ),
          ],
        ),
      ],
    );
    return Card(
      margin: const EdgeInsets.only(bottom: 16),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onEdit,
        child: LayoutBuilder(
          builder: (context, c) => c.maxWidth >= 720
              ? Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SizedBox(width: 260, height: 190, child: cover),
                    Expanded(
                      child: Padding(padding: const EdgeInsets.fromLTRB(24, 20, 16, 20), child: info),
                    ),
                    Padding(padding: const EdgeInsets.fromLTRB(0, 20, 16, 0), child: actions),
                  ],
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    AspectRatio(aspectRatio: 16 / 9, child: cover),
                    Padding(padding: const EdgeInsets.fromLTRB(16, 16, 16, 8), child: info),
                    Padding(padding: const EdgeInsets.fromLTRB(8, 0, 8, 12), child: actions),
                  ],
                ),
        ),
      ),
    );
  }
}

/// Blog-wide author (settings `blog.author` / `blog.author_avatar`), shown on every post.
class _BlogSettings extends StatefulWidget {
  const _BlogSettings();
  @override
  State<_BlogSettings> createState() => _BlogSettingsState();
}

class _BlogSettingsState extends State<_BlogSettings> {
  final _name = TextEditingController();
  String? _avatar; // `/media/...` URL, as the site uses it
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    api
        .get('/admin/settings')
        .then(
          (s) => mounted
              ? setState(() {
                  _name.text = s['blog.author'] ?? '';
                  _avatar = s['blog.author_avatar'];
                })
              : null,
          onError: (Object e) => mounted ? toast(context, '$e') : null,
        );
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _busy = true);
    try {
      // An empty value deletes the key.
      await api.put('/admin/settings', {'blog.author': _name.text.trim(), 'blog.author_avatar': _avatar ?? ''});
      if (mounted) toast(context, 'Налаштування блогу збережено');
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme, cs = Theme.of(context).colorScheme;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: ExpansionTile(
        leading: const Icon(Icons.settings_outlined),
        title: const Text('Налаштування блогу'),
        subtitle: const Text('Автор та аватар для всіх дописів'),
        shape: const Border(),
        collapsedShape: const Border(),
        childrenPadding: const EdgeInsets.fromLTRB(24, 4, 24, 24),
        expandedCrossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 24,
            runSpacing: 16,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              CircleAvatar(
                radius: 36,
                backgroundColor: cs.surfaceContainerHighest,
                foregroundImage: _avatar == null ? null : NetworkImage(mediaUrl(_avatar!)),
                child: Icon(Icons.person, size: 40, color: cs.outline),
              ),
              SizedBox(
                width: 360,
                child: TextField(
                  controller: _name,
                  decoration: const InputDecoration(
                    labelText: "Ім'я автора",
                    border: OutlineInputBorder(),
                    helperText: 'Показується біля дати на сторінці кожного допису',
                  ),
                ),
              ),
              Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      OutlinedButton.icon(
                        icon: const Icon(Icons.upload),
                        label: Text(_avatar == null ? 'Завантажити аватар' : 'Замінити аватар'),
                        onPressed: () async {
                          final k = await uploadPicked(context, 'blog', _images);
                          if (k != null) setState(() => _avatar = '/media/$k');
                        },
                      ),
                      if (_avatar != null)
                        TextButton(onPressed: () => setState(() => _avatar = null), child: const Text('Прибрати')),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text('Формати: jpg, png, webp', style: t.bodySmall?.copyWith(color: cs.outline)),
                ],
              ),
            ],
          ),
          const SizedBox(height: 20),
          FilledButton(onPressed: _busy ? null : _save, child: const Text('Зберегти')),
        ],
      ),
    );
  }
}

/// Post editor dialog: Content (title, date, body) and Settings & SEO, per language.
class PostEditor extends StatefulWidget {
  const PostEditor({super.key, this.post, this.tab = 0});
  final J? post;
  final int tab;

  @override
  State<PostEditor> createState() => _PostEditorState();
}

class _PostEditorState extends State<PostEditor> with SingleTickerProviderStateMixin {
  late final J d = widget.post == null
      ? {
          'published': false,
          'titleUk': '',
          'bodyHtmlUk': '',
          'slug': '',
          'publishedAt': DateTime.now().toUtc().toIso8601String(),
        }
      : jsonDecode(jsonEncode(widget.post)) as J; // deep copy
  late final _initial = jsonEncode(d);
  late final _tabs = TabController(length: 2, vsync: this, initialIndex: widget.tab);
  late final TextEditingController _slug = TextEditingController(text: d['slug'])
    ..addListener(() => d['slug'] = _slug.text.trim());
  late bool _autoSlug = _isNew; // slug follows the title until edited by hand
  String _lang = 'Uk'; // DTO key suffix: titleUk / titleEn …
  bool _busy = false;

  bool get _isNew => widget.post == null;
  bool get _wasPublished => widget.post?['published'] == true;

  @override
  void dispose() {
    _tabs.dispose();
    _slug.dispose();
    super.dispose();
  }

  Future<void> _close() async {
    if (jsonEncode(d) != _initial && !await confirm(context, 'Закрити без збереження змін?')) return;
    if (mounted) Navigator.pop(context, false);
  }

  Future<void> _save(bool publish) async {
    final (err, tab) = '${d['titleUk'] ?? ''}'.trim().isEmpty
        ? ('Вкажіть заголовок українською', 0)
        : blankHtml(d['bodyHtmlUk'])
        ? ('Додайте текст допису українською', 0)
        : !slugPattern.hasMatch('${d['slug']}')
        ? ('Вкажіть адресу допису: латиниця, цифри, дефіси', 1)
        : (null, 0);
    if (err != null) {
      _tabs.animateTo(tab);
      if (tab == 0) setState(() => _lang = 'Uk');
      return toast(context, err);
    }
    setState(() => _busy = true);
    try {
      final body = {...d, 'published': publish};
      await (_isNew ? api.post(_path, body) : api.put('$_path/${d['id']}', body));
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  Future<void> _delete() async {
    if (!await confirm(context, 'Видалити допис назавжди?')) return;
    try {
      await api.delete('$_path/${d['id']}');
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
  }

  Future<void> _pickDate() async {
    final p = await showDatePicker(
      context: context,
      helpText: 'Дата публікації',
      initialDate: DateTime.tryParse('${d['publishedAt']}')?.toLocal() ?? DateTime.now(),
      firstDate: DateTime(2015),
      lastDate: DateTime(2100),
    );
    if (p == null) return;
    // Today → now, so the post isn't held back till noon; other days → noon, safe from time-zone shifts.
    final at = DateUtils.isSameDay(p, DateTime.now()) ? DateTime.now() : DateTime(p.year, p.month, p.day, 12);
    setState(() => d['publishedAt'] = at.toUtc().toIso8601String());
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) => didPop ? null : _close(),
      child: Dialog(
        insetPadding: const EdgeInsets.all(24),
        clipBehavior: Clip.antiAlias,
        child: SizedBox(
          width: 1100,
          height: MediaQuery.sizeOf(context).height - 48,
          // Own messenger: toasts show inside the dialog, not under its barrier.
          child: ScaffoldMessenger(
            child: Scaffold(
              body: Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(24, 16, 12, 0),
                    child: Row(
                      children: [
                        Expanded(child: Text(_isNew ? 'Новий допис' : 'Редагування допису', style: t.headlineSmall)),
                        SegmentedButton<String>(
                          segments: const [
                            ButtonSegment(value: 'Uk', label: Text('Українська')),
                            ButtonSegment(value: 'En', label: Text('English')),
                          ],
                          selected: {_lang},
                          showSelectedIcon: false,
                          onSelectionChanged: (s) => setState(() => _lang = s.first),
                        ),
                        const SizedBox(width: 8),
                        IconButton(tooltip: 'Закрити', icon: const Icon(Icons.close), onPressed: _close),
                      ],
                    ),
                  ),
                  TabBar(
                    controller: _tabs,
                    isScrollable: true,
                    tabAlignment: TabAlignment.start,
                    tabs: const [
                      Tab(text: 'Контент'),
                      Tab(text: 'Налаштування й SEO'),
                    ],
                  ),
                  Expanded(
                    child: TabBarView(
                      controller: _tabs,
                      physics: const NeverScrollableScrollPhysics(),
                      children: [_content(), _seo()],
                    ),
                  ),
                  const Divider(height: 1),
                  Padding(padding: const EdgeInsets.fromLTRB(16, 12, 24, 12), child: _footer()),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _content() {
    final en = _lang == 'En';
    return Padding(
      padding: const EdgeInsets.fromLTRB(24, 20, 24, 16),
      child: Column(
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: TextFormField(
                  key: ValueKey('title$_lang'),
                  initialValue: d['title$_lang'] ?? '',
                  style: Theme.of(context).textTheme.titleLarge,
                  decoration: InputDecoration(
                    labelText: en ? 'Заголовок (EN)' : 'Заголовок *',
                    helperText: en ? 'Порожні поля EN — англійська версія сайту покаже український текст' : null,
                    border: const OutlineInputBorder(),
                  ),
                  onChanged: (v) {
                    d['title$_lang'] = v;
                    if (!en && _autoSlug) _slug.text = slugify(v);
                  },
                ),
              ),
              const SizedBox(width: 12),
              Tooltip(
                message: 'Дата публікації',
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(minimumSize: const Size(0, 60)),
                  icon: const Icon(Icons.calendar_today_outlined, size: 18),
                  label: Text(fmtDate(d['publishedAt'], time: false)),
                  onPressed: _pickDate,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: HtmlField(
              F('bodyHtml$_lang', en ? 'Текст (EN)' : 'Текст', t: T.html, required: !en, folder: 'blog'),
              d,
              key: ValueKey('body$_lang'),
              height: null,
            ),
          ),
        ],
      ),
    );
  }

  Widget _seo() {
    final l = _lang, lang = ' · ${l.toUpperCase()}';
    final t = Theme.of(context).textTheme, cs = Theme.of(context).colorScheme;
    final title = '${d['title$l'] ?? ''}'.trim().isNotEmpty ? d['title$l'] : d['titleUk'];
    final custom = d['seoTitle$l'] != null;
    Widget label(String text, [String? tip]) => Padding(
      padding: const EdgeInsets.only(top: 24, bottom: 8),
      child: Row(
        children: [
          Text(text.toUpperCase(), style: t.labelLarge?.copyWith(fontWeight: FontWeight.w600)),
          if (tip != null)
            Tooltip(
              message: tip,
              child: Padding(
                padding: const EdgeInsets.only(left: 6),
                child: Icon(Icons.help_outline, size: 16, color: cs.outline),
              ),
            ),
        ],
      ),
    );
    Widget field(String key, {String? hint, String? helper, int lines = 1}) => TextFormField(
      key: ValueKey(key),
      initialValue: d[key] ?? '',
      minLines: lines,
      maxLines: lines == 1 ? 1 : lines + 3,
      decoration: InputDecoration(hintText: hint, helperText: helper, border: const OutlineInputBorder()),
      onChanged: (v) => d[key] = v,
    );
    Widget check(String key, String title, String subtitle) => CheckboxListTile(
      value: d[key] == true,
      onChanged: (v) => setState(() => d[key] = v),
      controlAffinity: ListTileControlAffinity.leading,
      contentPadding: EdgeInsets.zero,
      title: Text(title),
      subtitle: Text(subtitle),
    );
    final cover = d['coverKey'] as String?;
    return ListView(
      padding: const EdgeInsets.fromLTRB(24, 0, 24, 32),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 820),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                label(
                  'Заголовок сторінки$lang',
                  'У вкладці браузера, у пошуку Google і при поширенні. За замовчуванням — заголовок допису',
                ),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: custom
                          ? field('seoTitle$l', helper: 'Оптимально до 60 символів')
                          : TextFormField(
                              key: ValueKey('seoOff$l'),
                              enabled: false,
                              decoration: InputDecoration(hintText: title, border: const OutlineInputBorder()),
                            ),
                    ),
                    const SizedBox(width: 16),
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Switch(value: custom, onChanged: (v) => setState(() => d['seoTitle$l'] = v ? title : null)),
                          const SizedBox(width: 8),
                          const Text('Свій заголовок'),
                        ],
                      ),
                    ),
                  ],
                ),
                label('Адреса допису', 'Частина посилання після /blog/'),
                TextFormField(
                  controller: _slug,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  decoration: InputDecoration(
                    prefixText: '/blog/',
                    border: const OutlineInputBorder(),
                    helperText: _isNew
                        ? 'Латиниця, цифри, дефіси. Поки не зміните вручну — створюється із заголовка'
                        : 'Зміна адреси зламає вже поширені посилання на допис',
                  ),
                  validator: (v) => slugPattern.hasMatch(v!.trim()) ? null : 'Лише латиниця a-z, цифри 0-9 і дефіси',
                  onChanged: (_) => _autoSlug = false,
                ),
                const SizedBox(height: 12),
                check(
                  'noIndex',
                  'Сховати з результатів пошуку',
                  'Пошуковики не показуватимуть допис (noindex), його не буде в sitemap. У блозі на сайті він лишається',
                ),
                check(
                  'noFollow',
                  'Не переходити за посиланнями в дописі',
                  'Пошуковики не враховуватимуть посилання з цього допису (nofollow)',
                ),
                label('Обкладинка (прев\'ю допису)'),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    border: Border.all(color: cs.outlineVariant),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Row(
                    children: [
                      ClipRRect(
                        borderRadius: BorderRadius.circular(6),
                        child: cover == null
                            ? Container(
                                width: 160,
                                height: 90,
                                color: cs.surfaceContainerHighest,
                                child: Icon(Icons.image_outlined, color: cs.outline),
                              )
                            : Image.network(mediaUrl(cover), width: 160, height: 90, fit: BoxFit.cover),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Text(
                          'У списку дописів, угорі допису і в прев\'ю при поширенні в соцмережах.\n'
                          'Рекомендовано 1200×675 (16:9); jpg, png або webp.',
                          style: t.bodySmall?.copyWith(color: cs.onSurfaceVariant),
                        ),
                      ),
                      TextButton.icon(
                        icon: const Icon(Icons.upload),
                        label: Text(cover == null ? 'Завантажити' : 'Замінити'),
                        onPressed: () async {
                          final k = await uploadPicked(context, 'blog', _images);
                          if (k != null) setState(() => d['coverKey'] = k);
                        },
                      ),
                      if (cover != null)
                        IconButton(
                          tooltip: 'Прибрати',
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () => setState(() => d['coverKey'] = null),
                        ),
                    ],
                  ),
                ),
                label('Анонс$lang'),
                field(
                  'excerpt$l',
                  lines: 2,
                  hint: 'Коротко про що допис',
                  helper: 'Показується під заголовком у списку дописів на сайті',
                ),
                label('Опис для пошуку$lang', 'Текст під заголовком у Google і в прев\'ю при поширенні'),
                field(
                  'seoDesc$l',
                  lines: 3,
                  hint: 'Текст, що з\'являється в пошуку та при поширенні',
                  helper: 'Оптимально до 160 символів. Якщо порожньо — анонс або початок тексту',
                ),
                label('Ключові слова$lang'),
                field('seoKeywords$l', hint: 'вітрогенератор, сонячна станція', helper: 'Через кому'),
                label('Автор'),
                field(
                  'author',
                  hint: 'Автор блогу',
                  helper: 'Лише якщо автор цього допису інший, ніж у налаштуваннях блогу',
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _footer() {
    final scheduled = _scheduled({...d, 'published': true});
    final cs = Theme.of(context).colorScheme;
    return Row(
      children: [
        if (!_isNew)
          IconButton(
            tooltip: 'Видалити допис',
            color: cs.error,
            icon: const Icon(Icons.delete_outline),
            onPressed: _busy ? null : _delete,
          ),
        const Spacer(),
        Flexible(
          flex: 4,
          child: Wrap(
            alignment: WrapAlignment.end,
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: 12,
            runSpacing: 8,
            children: [
              if (scheduled)
                Text(
                  'Зʼявиться на сайті ${fmtDate(d['publishedAt'], time: false)}',
                  style: TextStyle(color: cs.onSurfaceVariant),
                ),
              OutlinedButton(
                onPressed: _busy ? null : () => _save(false),
                child: Text(_wasPublished ? 'Зняти з публікації' : 'Зберегти чернетку'),
              ),
              FilledButton(
                onPressed: _busy ? null : () => _save(true),
                child: Text(
                  _wasPublished
                      ? 'Зберегти'
                      : scheduled
                      ? 'Запланувати'
                      : 'Опублікувати',
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
