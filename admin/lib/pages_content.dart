import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'form.dart';
import 'list.dart';
import 'main.dart' show nav;

// ── Blog (Editor) ──

const _blog = '/admin/blog';

const _blogFields = [
  F.section('Основне'),
  F('titleUk', 'Заголовок (UK)', required: true),
  F('titleEn', 'Заголовок (EN)'),
  F('slug', 'Адреса (slug)', t: T.slug, required: true, hint: 'uagreen.co/blog/<slug>'),
  F('publishedAt', 'Дата публікації', t: T.date),
  F('author', 'Автор'),
  F('published', 'Опубліковано', t: T.bool),
  F('coverKey', 'Обкладинка', t: T.image, folder: 'blog'),
  F.section('Текст'),
  F('excerptUk', 'Анонс (UK)', t: T.multiline, hint: 'Показується в списку статей'),
  F('excerptEn', 'Анонс (EN)', t: T.multiline),
  F('bodyHtmlUk', 'Текст (UK)', t: T.html, required: true, folder: 'blog'),
  F('bodyHtmlEn', 'Текст (EN)', t: T.html, folder: 'blog'),
  F.section('SEO', hint: 'Порожньо — підставляються заголовок і анонс'),
  F('seoTitleUk', 'SEO title (UK)'),
  F('seoTitleEn', 'SEO title (EN)'),
  F('seoDescUk', 'SEO description (UK)', t: T.multiline),
  F('seoDescEn', 'SEO description (EN)', t: T.multiline),
];

class BlogPage extends StatefulWidget {
  const BlogPage({super.key});
  @override
  State<BlogPage> createState() => _BlogPageState();
}

class _BlogPageState extends State<BlogPage> {
  final _list = GlobalKey<PagedListState>();
  String? _filter;

  Future<void> _edit(J? row) async {
    if (await editResource(
          context,
          path: _blog,
          noun: 'стаття',
          fields: _blogFields,
          row: row,
          blank: () => {'published': false, 'bodyHtmlUk': ''},
          title: (d) => d['titleUk'] ?? 'Стаття',
          sitePath: (d) => '/blog/${d['slug']}',
          published: (d) => d['published'] == true,
        ) ==
        true) {
      _list.currentState?.reload();
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet(_blog, {'filter': _filter}),
    query: {'filter': _filter},
    searchHint: 'Заголовок, slug або автор',
    emptyIcon: Icons.article_outlined,
    filters: [
      filterChip(
        icon: Icons.filter_list,
        tooltip: 'Стан',
        allLabel: 'Усі статті',
        options: const {'published': 'Опубліковані', 'draft': 'Чернетки'},
        value: _filter,
        onSelected: (v) => setState(() => _filter = v),
      ),
    ],
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Стаття'))],
    item: (context, r, list) => AppListCard(
      leading: thumb(r['coverUrl']),
      title: Text('${r['titleUk']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Wrap(
        spacing: AppSpacing.sm,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text([fmtDate(r['publishedAt'], time: false), r['author']].nonNulls.join(' · ')),
          if (r['published'] != true) chip(context, 'Чернетка', 'neutral'),
          if (r['titleEn'] == null) chip(context, 'без EN', 'warning'),
        ],
      ),
      trailing: IconButton(
        tooltip: 'Видалити',
        icon: const Icon(Icons.delete_outline),
        onPressed: () async {
          if (await confirmDelete(context, '${r['titleUk']}', '$_blog/${r['id']}', warning: 'Стаття зникне з сайту. ')) {
            list.removeWhere((x) => x['id'] == r['id']);
          }
        },
      ),
      onTap: () => _edit(r),
    ),
  );
}

// ── Vacancies (HR) ──

const _vac = '/admin/vacancies';

const _vacancyFields = [
  F.section('Основне'),
  F('titleUk', 'Назва (UK)', required: true),
  F('titleEn', 'Назва (EN)'),
  F('slug', 'Адреса (slug)', t: T.slug, required: true, hint: 'uagreen.co/vacancies/<slug>'),
  F('department', 'Відділ', hint: 'Вкладка на сторінці вакансій: Менеджмент / Слюсарі / Інженери / Програмування'),
  F('employmentType', 'Зайнятість', hint: 'Напр. повна, часткова'),
  F('city', 'Місто'),
  F('experience', 'Досвід'),
  F('language', 'Мова'),
  F('isActive', 'Активна (видно на сайті)', t: T.bool),
  F('sort', 'Порядок', t: T.int),
  F('publishedAt', 'Дата публікації', t: T.date),
  F.section('Опис'),
  F('requirementsHtmlUk', 'Вимоги (UK)', t: T.html),
  F('requirementsHtmlEn', 'Вимоги (EN)', t: T.html),
  F('niceToHaveHtmlUk', 'Буде плюсом (UK)', t: T.html),
  F('niceToHaveHtmlEn', 'Буде плюсом (EN)', t: T.html),
  F('conditionsHtmlUk', 'Умови (UK)', t: T.html),
  F('conditionsHtmlEn', 'Умови (EN)', t: T.html),
];

class VacanciesPage extends StatefulWidget {
  const VacanciesPage({super.key});
  @override
  State<VacanciesPage> createState() => _VacanciesPageState();
}

class _VacanciesPageState extends State<VacanciesPage> {
  final _list = GlobalKey<PagedListState>();
  String? _filter;

  static String _warn(J r) {
    final n = (r['applications'] as num? ?? 0).toInt();
    return n == 0 ? '' : '$n відгук(ів) залишаться, але без прив\'язки до вакансії. ';
  }

  Future<void> _edit(J? row) async {
    if (await editResource(
          context,
          path: _vac,
          noun: 'вакансія',
          fields: _vacancyFields,
          row: row,
          blank: () => {'isActive': true, 'sort': 0, 'city': 'Львів', 'department': '', 'employmentType': ''},
          title: (d) => d['titleUk'] ?? 'Вакансія',
          sitePath: (d) => '/vacancies/${d['slug']}',
          published: (d) => d['isActive'] == true,
          deleteWarning: _warn,
        ) ==
        true) {
      _list.currentState?.reload();
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet(_vac, {'filter': _filter}),
    query: {'filter': _filter},
    searchHint: 'Назва, відділ або місто',
    emptyIcon: Icons.work_outline,
    filters: [
      filterChip(
        icon: Icons.filter_list,
        tooltip: 'Стан',
        allLabel: 'Усі вакансії',
        options: const {'active': 'Активні', 'inactive': 'Неактивні'},
        value: _filter,
        onSelected: (v) => setState(() => _filter = v),
      ),
    ],
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Вакансія'))],
    item: (context, r, list) => AppListCard(
      leading: const Icon(Icons.work_outline),
      title: Text('${r['titleUk']}'),
      subtitle: Wrap(
        spacing: AppSpacing.sm,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text([r['department'], r['city'], r['employmentType']].where((s) => '${s ?? ''}'.isNotEmpty).join(' · ')),
          if (r['isActive'] != true) chip(context, 'Неактивна', 'neutral'),
        ],
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          MetaChip(
            label: '${r['applications']} відгуків',
            icon: Icons.badge_outlined,
            onTap: () => nav.go('applications', {'vacancyId': '${r['id']}'}),
          ),
          IconButton(
            tooltip: 'Видалити',
            icon: const Icon(Icons.delete_outline),
            onPressed: () async {
              if (await confirmDelete(context, '${r['titleUk']}', '$_vac/${r['id']}', warning: _warn(r))) {
                list.removeWhere((x) => x['id'] == r['id']);
              }
            },
          ),
        ],
      ),
      onTap: () => _edit(r),
    ),
  );
}

// ── Content blocks (Editor) ──

/// Where each known block shows up on the site — the key alone says little.
const contentKeys = {
  'page.about': 'Сторінка «Про нас»',
  'page.contacts': 'Сторінка «Контакти»',
  'page.oplata': 'Сторінка «Оплата і доставка»',
  'page.terms': 'Умови використання сайту',
  'home.projects': 'Головна: блок «Проєкти»',
  'home.testimonials': 'Головна: відгуки',
  'oberig.home': 'Головна: блок «Оберіг»',
  'oberig.product': 'Товар: блок «Моніторинг Оберіг»',
  'oberig.order': 'Сторінка замовлення: блок «Оберіг»',
};

const _contentBody = [
  F('title', 'Заголовок'),
  F('bodyHtml', 'Текст', t: T.html, folder: 'content'),
];

Future<void> _saveContent(J d) =>
    api.put('/admin/content/${d['key']}/${d['lang']}', {'title': d['title'], 'bodyHtml': d['bodyHtml'] ?? ''});

/// Edit (or create) one block by key+lang, from anywhere (settings → Oberig texts).
Future<bool?> editContentBlock(BuildContext context, String key, String lang) async {
  J data = {'key': key, 'lang': lang};
  try {
    final page = await api.get('/admin/content', {'q': key, 'lang': lang, 'pageSize': 50}) as J;
    data = (page['items'] as List).cast<J>().firstWhere((x) => x['key'] == key, orElse: () => data);
  } catch (_) {
    // new block
  }
  if (!context.mounted) return false;
  return openEditor(context, '${contentKeys[key] ?? key} (${lang.toUpperCase()})', _contentBody, deepCopy(data), _saveContent,
      subtitle: key);
}

class ContentPage extends StatefulWidget {
  const ContentPage({super.key});
  @override
  State<ContentPage> createState() => _ContentPageState();
}

class _ContentPageState extends State<ContentPage> {
  final _list = GlobalKey<PagedListState>();
  String? _lang;

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet('/admin/content', {'lang': _lang}),
    query: {'lang': _lang},
    searchHint: 'Ключ, заголовок або текст',
    emptyIcon: Icons.web_outlined,
    filters: [
      filterChip(
        icon: Icons.translate,
        tooltip: 'Мова',
        allLabel: 'Усі мови',
        options: const {'uk': 'Українська', 'en': 'English'},
        value: _lang,
        onSelected: (v) => setState(() => _lang = v),
      ),
    ],
    actions: [
      FilledButton.icon(
        icon: const Icon(Icons.add),
        label: const Text('Блок'),
        onPressed: () async {
          if (await openEditor(
                context,
                'Новий блок',
                [
                  F('key', 'Ключ', required: true, hint: 'Напр. page.about; відомі: ${contentKeys.keys.join(', ')}'),
                  F('lang', 'Мова', t: T.select, required: true, choices: () async => [('uk', 'Українська'), ('en', 'English')]),
                  ..._contentBody,
                ],
                {},
                _saveContent,
              ) ==
              true) {
            _list.currentState?.reload();
          }
        },
      ),
    ],
    item: (context, r, list) => AppListCard(
      leading: CircleAvatar(
        backgroundColor: context.palette.chipBg,
        child: Text('${r['lang']}'.toUpperCase(), style: Theme.of(context).textTheme.labelSmall),
      ),
      title: Text(contentKeys[r['key']] ?? '${r['title'] ?? r['key']}'),
      subtitle: Text('${r['key']}${r['title'] == null ? '' : ' · ${r['title']}'}', maxLines: 1, overflow: TextOverflow.ellipsis),
      trailing: IconButton(
        tooltip: 'Видалити',
        icon: const Icon(Icons.delete_outline),
        onPressed: () async {
          if (await confirmDelete(context, '${r['key']} (${r['lang']})', '/admin/content/${r['key']}/${r['lang']}',
              warning: 'Сайт покаже стандартний текст або нічого. ')) {
            list.removeWhere((x) => x['key'] == r['key'] && x['lang'] == r['lang']);
          }
        },
      ),
      onTap: () async {
        if (await openEditor(context, '${contentKeys[r['key']] ?? r['key']} (${'${r['lang']}'.toUpperCase()})', _contentBody,
                deepCopy(r), _saveContent, subtitle: '${r['key']}') ==
            true) {
          list.reload();
        }
      },
    ),
  );
}

// ── Redirects (Editor) ──

class RedirectsPage extends StatefulWidget {
  const RedirectsPage({super.key});
  @override
  State<RedirectsPage> createState() => _RedirectsPageState();
}

class _RedirectsPageState extends State<RedirectsPage> {
  final _list = GlobalKey<PagedListState>();

  Future<void> _edit(J? r) async {
    final saved = await ResponsiveFormDialog.show<bool>(context, builder: (_) => _RedirectDialog(row: r));
    if (saved == true) _list.currentState?.reload();
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet('/admin/redirects'),
    pageSize: 50,
    searchHint: 'Стара або нова адреса',
    emptyIcon: Icons.alt_route,
    emptyText: 'Редиректів немає',
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Редирект'))],
    item: (context, r, list) => AppListCard(
      leading: const Icon(Icons.alt_route),
      title: Text('${r['from']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('→ ${r['to']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      trailing: IconButton(
        tooltip: 'Видалити',
        icon: const Icon(Icons.delete_outline),
        onPressed: () async {
          if (await confirmDelete(context, '${r['from']}', '/admin/redirects/item?from=${Uri.encodeQueryComponent('${r['from']}')}',
              warning: 'Стара адреса почне віддавати 404. ')) {
            list.removeWhere((x) => x['from'] == r['from']);
          }
        },
      ),
      onTap: () => _edit(r),
    ),
  );
}

class _RedirectDialog extends StatefulWidget {
  const _RedirectDialog({this.row});
  final J? row;
  @override
  State<_RedirectDialog> createState() => _RedirectDialogState();
}

class _RedirectDialogState extends State<_RedirectDialog> with UnsavedChangesGuard {
  final _form = GlobalKey<FormState>();
  late final J _d = {'from': widget.row?['from'] ?? '', 'to': widget.row?['to'] ?? ''};
  String? _error;
  bool _busy = false;

  @override
  bool get isDirty => _d['from'] != (widget.row?['from'] ?? '') || _d['to'] != (widget.row?['to'] ?? '');

  String? _path(String? v) => v!.trim().startsWith('/') ? null : 'Адреса має починатися з /';

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _busy = true);
    try {
      await api.post('/admin/redirects/item', {..._d, 'original': widget.row?['from']});
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) => guard(
    child: ResponsiveFormDialog(
      title: widget.row == null ? 'Новий редирект' : 'Редирект',
      content: SizedBox(
        width: AppSizes.dialogContentWidthLg,
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (_error != null) ...[FormBanner(_error!), AppSpacing.gapMd],
              TextFormField(
                initialValue: _d['from'],
                decoration: const InputDecoration(labelText: 'Стара адреса', hintText: '/stara-storinka', helperText: 'Шлях без домену'),
                validator: _path,
                onChanged: (v) => _d['from'] = v.trim(),
              ),
              AppSpacing.gapMd,
              TextFormField(
                initialValue: _d['to'],
                decoration: const InputDecoration(labelText: 'Нова адреса', hintText: '/shop/vitryak-k5'),
                validator: _path,
                onChanged: (v) => _d['to'] = v.trim(),
              ),
              AppSpacing.gapSm,
              Text('Відвідувачі й пошуковики переходитимуть зі старої адреси на нову (301).',
                  style: Theme.of(context).textTheme.bodySmall),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () async {
            if (await confirmClose() && context.mounted) Navigator.pop(context);
          },
          child: const Text('Скасувати'),
        ),
        AppButton(label: 'Зберегти', isLoading: _busy, onPressed: _save),
      ],
    ),
  );
}
