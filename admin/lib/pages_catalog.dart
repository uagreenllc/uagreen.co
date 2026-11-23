import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';
import 'list.dart';

const _cats = '/admin/catalog/categories', _products = '/admin/catalog/products';

Future<List<J>> _allCategories() async => ((await api.get(_cats, {'pageSize': 200}) as J)['items'] as List).cast<J>();

Future<List<(Object?, String)>> _categoryChoices() async => [for (final c in await _allCategories()) (c['id'], c['nameUk'] as String)];

const productFields = [
  F.section('Основне'),
  F('nameUk', 'Назва (UK)', required: true),
  F('nameEn', 'Назва (EN)', hint: 'Порожньо — на англійській версії буде українська назва'),
  F('slug', 'Адреса (slug)', t: T.slug, required: true, hint: 'uagreen.co/shop/<slug>'),
  F('sku', 'Артикул'),
  F('categoryId', 'Категорія', t: T.select, choices: _categoryChoices),
  F.section('Ціна і показ'),
  F('price', 'Ціна', t: T.money, required: true),
  F('oldPrice', 'Стара ціна', t: T.money, hint: 'Буде показана закресленою'),
  F('published', 'Опубліковано на сайті', t: T.bool),
  F('inStock', 'В наявності', t: T.bool, hint: 'Без наявності товар видно, але кнопка «Купити» неактивна'),
  F('isFeatured', 'Показувати на головній', t: T.bool),
  F('oberigMonitoring', 'Блок «Моніторинг Оберіг»', t: T.bool, hint: 'Для станцій: на сторінці товару — блок про портал Оберіг'),
  F('sort', 'Порядок у каталозі', t: T.int, hint: 'Менше число — вище'),
  F.section('Фото і файли'),
  F('images', 'Фото (перше — головне)', t: T.images, folder: 'products'),
  F('files', 'Файли для завантаження', t: T.files),
  F.section('Опції', hint: 'Комплектація, розмір… Ціна значення додається до ціни товару'),
  F('options', 'Опції', t: T.options),
  F.section('Опис'),
  F('shortDescUk', 'Короткий опис (UK)', t: T.multiline),
  F('shortDescEn', 'Короткий опис (EN)', t: T.multiline),
  F('descriptionHtmlUk', 'Опис (UK)', t: T.html, folder: 'products'),
  F('descriptionHtmlEn', 'Опис (EN)', t: T.html, folder: 'products'),
  F('specsHtmlUk', 'Характеристики (UK)', t: T.html, folder: 'products'),
  F('specsHtmlEn', 'Характеристики (EN)', t: T.html, folder: 'products'),
  F('faqHtmlUk', 'FAQ (UK)', t: T.html, folder: 'products'),
  F('faqHtmlEn', 'FAQ (EN)', t: T.html, folder: 'products'),
  F.section('SEO', hint: 'Порожньо — підставляються назва і короткий опис'),
  F('seoTitleUk', 'SEO title (UK)'),
  F('seoTitleEn', 'SEO title (EN)'),
  F('seoDescUk', 'SEO description (UK)', t: T.multiline),
  F('seoDescEn', 'SEO description (EN)', t: T.multiline),
];

J _blankProduct() => {
  'price': 0, 'inStock': true, 'published': false, 'isFeatured': false, 'oberigMonitoring': false, 'sort': 0,
  'images': [], 'files': [], 'options': [],
};

class ProductsPage extends StatefulWidget {
  const ProductsPage({super.key});
  @override
  State<ProductsPage> createState() => _ProductsPageState();
}

class _ProductsPageState extends State<ProductsPage> {
  final _list = GlobalKey<PagedListState>();
  String? _filter, _category;
  Map<String, String> _categories = {};

  static const _filters = {
    'published': 'Опубліковані',
    'draft': 'Чернетки',
    'outOfStock': 'Немає в наявності',
    'featured': 'На головній',
    'noPhoto': 'Без фото',
  };

  @override
  void initState() {
    super.initState();
    _allCategories().then((c) {
      if (mounted) setState(() => _categories = {for (final x in c) x['id'] as String: x['nameUk'] as String});
    }).ignore();
  }

  Future<void> _edit(J? row) async {
    final saved = await editResource(
      context,
      path: _products,
      noun: 'товар',
      fields: productFields,
      row: row,
      blank: _blankProduct,
      detail: true,
      title: (d) => d['nameUk'] ?? 'Товар',
      sitePath: (d) => '/shop/${d['slug']}',
      published: (d) => d['published'] == true,
      extraActions: [(c, d) => IconButton(tooltip: 'Дублювати', icon: const Icon(Icons.copy_all_outlined), onPressed: () => _duplicate(c, d))],
    );
    if (saved == true) _list.currentState?.reload();
  }

  Future<void> _duplicate(BuildContext c, J row) async {
    try {
      final copy = await api.post('$_products/${row['id']}/duplicate', null) as J;
      if (!c.mounted) return;
      AppFeedback.showSuccess(c, 'Створено копію-чернетку');
      Navigator.pop(c, true); // close the original's editor (if open) …
      _edit(copy); // … and open the copy
    } catch (e) {
      if (c.mounted) AppFeedback.showError(c, '$e');
    }
  }

  Future<void> _toggle(J row, String field, bool value) async {
    if (field == 'published' && !value) {
      final ok = await showConfirmDialog(
        context,
        title: 'Зняти «${row['nameUk']}» з сайту?',
        message: 'Сторінка товару перестане відкриватися, товар зникне з каталогу і з головної.',
        confirmLabel: 'Зняти з сайту',
        destructive: false,
      );
      if (!ok) return;
    }
    try {
      _list.currentState?.replace(await api.patch('$_products/${row['id']}', {field: value}) as J);
      summary.refresh();
    } catch (e) {
      if (mounted) AppFeedback.showError(context, '$e');
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet(_products, {'filter': _filter, 'categoryId': _category}),
    query: {'filter': _filter, 'categoryId': _category},
    searchHint: 'Назва, артикул або slug',
    emptyIcon: Icons.inventory_2_outlined,
    filters: [
      filterChip(
        icon: Icons.filter_list,
        tooltip: 'Стан',
        allLabel: 'Усі товари',
        options: _filters,
        value: _filter,
        onSelected: (v) => setState(() => _filter = v),
      ),
      if (_categories.isNotEmpty)
        filterChip(
          icon: Icons.category_outlined,
          tooltip: 'Категорія',
          allLabel: 'Усі категорії',
          options: _categories,
          value: _category,
          onSelected: (v) => setState(() => _category = v),
        ),
    ],
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Товар'))],
    item: (context, r, list) {
      final theme = Theme.of(context);
      return AppListCard(
        leading: thumb(r['image']),
        title: Text('${r['nameUk']}', maxLines: 1, overflow: TextOverflow.ellipsis),
        subtitle: Wrap(
          spacing: AppSpacing.sm,
          runSpacing: AppSpacing.xs,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text([r['sku'], r['categoryName'] ?? 'Без категорії'].nonNulls.join(' · ')),
            if (r['published'] != true) chip(context, 'Чернетка', 'neutral'),
            if (r['inStock'] != true) chip(context, 'Немає в наявності', 'warning'),
            if (r['isFeatured'] == true) chip(context, 'На головній', 'info', icon: Icons.star_outline),
            if (r['oberigMonitoring'] == true) chip(context, 'Оберіг', 'success', icon: Icons.monitor_heart_outlined),
            if ((r['imageCount'] as num? ?? 0) == 0) chip(context, 'Без фото', 'error'),
          ],
        ),
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(money(r['price']), style: theme.textTheme.titleSmall?.numeric),
                if (r['oldPrice'] != null)
                  Text(money(r['oldPrice']),
                      style: theme.textTheme.bodySmall?.copyWith(decoration: TextDecoration.lineThrough)),
              ],
            ),
            PopupMenuButton<String>(
              tooltip: 'Дії',
              onSelected: (a) async {
                switch (a) {
                  case 'published' || 'inStock' || 'isFeatured' || 'oberigMonitoring':
                    _toggle(r, a, r[a] != true);
                  case 'site':
                    if (summary.link('site') case final s?) browser.openTab('$s/shop/${r['slug']}');
                  case 'duplicate':
                    if (await attempt(context, () => api.post('$_products/${r['id']}/duplicate', null), ok: 'Створено копію-чернетку')) {
                      list.reload();
                    }
                  case 'delete':
                    if (await confirmDelete(context, '${r['nameUk']}', '$_products/${r['id']}',
                        warning: 'Товар зникне з сайту; у вже оформлених замовленнях назва й ціна збережуться. ')) {
                      list.removeWhere((x) => x['id'] == r['id']);
                    }
                }
              },
              itemBuilder: (_) => [
                CheckedPopupMenuItem(value: 'published', checked: r['published'] == true, child: const Text('На сайті')),
                CheckedPopupMenuItem(value: 'inStock', checked: r['inStock'] == true, child: const Text('В наявності')),
                CheckedPopupMenuItem(value: 'isFeatured', checked: r['isFeatured'] == true, child: const Text('На головній')),
                CheckedPopupMenuItem(
                    value: 'oberigMonitoring', checked: r['oberigMonitoring'] == true, child: const Text('Блок «Оберіг»')),
                const PopupMenuDivider(),
                if (r['published'] == true)
                  const PopupMenuItem(value: 'site', child: ListTile(leading: Icon(Icons.open_in_new), title: Text('Відкрити на сайті'))),
                const PopupMenuItem(value: 'duplicate', child: ListTile(leading: Icon(Icons.copy_all_outlined), title: Text('Дублювати'))),
                PopupMenuItem(
                  value: 'delete',
                  child: ListTile(
                    leading: Icon(Icons.delete_outline, color: theme.colorScheme.error),
                    title: Text('Видалити', style: TextStyle(color: theme.colorScheme.error)),
                  ),
                ),
              ],
            ),
          ],
        ),
        onTap: () => _edit(r),
      );
    },
  );
}

const _categoryFields = [
  F.section('Основне'),
  F('nameUk', 'Назва (UK)', required: true),
  F('nameEn', 'Назва (EN)'),
  F('nameCs', 'Назва (CS)'),
  F('slug', 'Адреса (slug)', t: T.slug, required: true, hint: 'uagreen.co/shop/cat/<slug>'),
  F('sort', 'Порядок', t: T.int, hint: 'Менше число — вище в меню магазину'),
  F('imageKey', 'Зображення', t: T.image, folder: 'categories'),
  F.section('Опис', hint: 'Текст над товарами на сторінці категорії'),
  F('descriptionHtmlUk', 'Опис (UK)', t: T.html, folder: 'categories'),
  F('descriptionHtmlEn', 'Опис (EN)', t: T.html, folder: 'categories'),
  F.section('SEO', hint: 'Порожньо — підставляються назва і опис'),
  F('seoTitleUk', 'SEO title (UK)'),
  F('seoTitleEn', 'SEO title (EN)'),
  F('seoDescUk', 'SEO description (UK)', t: T.multiline),
  F('seoDescEn', 'SEO description (EN)', t: T.multiline),
];

class CategoriesPage extends StatefulWidget {
  const CategoriesPage({super.key});
  @override
  State<CategoriesPage> createState() => _CategoriesPageState();
}

class _CategoriesPageState extends State<CategoriesPage> {
  final _list = GlobalKey<PagedListState>();

  static String _warn(J r) {
    final n = (r['productCount'] as num? ?? 0).toInt();
    return n == 0 ? '' : '$n товар(ів) залишаться без категорії і зникнуть з її сторінки. ';
  }

  Future<void> _edit(J? row) async {
    if (await editResource(
          context,
          path: _cats,
          noun: 'категорія',
          fields: _categoryFields,
          row: row,
          blank: () => {'sort': 0},
          title: (d) => d['nameUk'] ?? 'Категорія',
          sitePath: (d) => '/shop/cat/${d['slug']}',
          deleteWarning: _warn,
        ) ==
        true) {
      _list.currentState?.reload();
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet(_cats),
    searchHint: 'Назва або slug',
    emptyIcon: Icons.category_outlined,
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Категорія'))],
    item: (context, r, list) => AppListCard(
      leading: thumb(r['imageUrl']),
      title: Text('${r['nameUk']}'),
      subtitle: Text('/shop/cat/${r['slug']} · порядок ${r['sort']}'),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          MetaChip(label: '${r['productCount']} товарів', icon: Icons.inventory_2_outlined),
          IconButton(
            tooltip: 'Видалити',
            icon: const Icon(Icons.delete_outline),
            onPressed: () async {
              if (await confirmDelete(context, '${r['nameUk']}', '$_cats/${r['id']}', warning: _warn(r))) {
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
