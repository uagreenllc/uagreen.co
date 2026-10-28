import 'dart:convert';

import 'package:flutter/material.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';

class Col {
  const Col(this.label, this.value);
  final String label;

  /// String/num → text, Widget → as is.
  final Object? Function(J row) value;
}

/// Table of rows with search, add, open (tap) and delete.
class ListPage extends StatefulWidget {
  const ListPage({
    super.key,
    required this.load,
    required this.cols,
    this.open,
    this.add,
    this.delete,
    this.actions,
    this.header,
  });
  final Future<List> Function() load;
  final List<Col> cols;
  final Future<bool?> Function(BuildContext context, J row)? open;
  final Future<bool?> Function(BuildContext context)? add;
  final Future<void> Function(J row)? delete;
  final List<Widget> Function(J row, VoidCallback reload)? actions;
  final Widget? header;

  @override
  State<ListPage> createState() => _ListPageState();
}

class _ListPageState extends State<ListPage> {
  late Future<List> _rows = widget.load();
  final _hScroll = ScrollController();
  String _q = '';

  @override
  void dispose() {
    _hScroll.dispose();
    super.dispose();
  }

  void _reload() => setState(() => _rows = widget.load());

  String _text(Object? v) => v is Widget ? '' : '${v ?? ''}';

  Future<void> _run(Future<bool?> Function() fn) async {
    try {
      if (await fn() == true) _reload();
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
  }

  @override
  Widget build(BuildContext context) {
    final w = widget;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Wrap(
            spacing: 12,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 280,
                child: TextField(
                  decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Пошук', isDense: true),
                  onChanged: (v) => setState(() => _q = v.toLowerCase()),
                ),
              ),
              ?w.header,
              IconButton(tooltip: 'Оновити', onPressed: _reload, icon: const Icon(Icons.refresh)),
              if (w.add != null)
                FilledButton.icon(
                  onPressed: () => _run(() => w.add!(context)),
                  icon: const Icon(Icons.add),
                  label: const Text('Додати'),
                ),
            ],
          ),
        ),
        Expanded(
          child: FutureBuilder(
            future: _rows,
            builder: (context, s) {
              if (s.hasError) return Center(child: Text('${s.error}'));
              if (!s.hasData) return const Center(child: CircularProgressIndicator());
              final rows = s.data!
                  .cast<J>()
                  .where((r) => _q.isEmpty || w.cols.any((c) => _text(c.value(r)).toLowerCase().contains(_q)))
                  .toList();
              if (rows.isEmpty) return const Center(child: Text('Порожньо'));
              return SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: Scrollbar(
                  controller: _hScroll,
                  thumbVisibility: true,
                  child: SingleChildScrollView(
                    controller: _hScroll,
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.only(bottom: 12),
                    child: DataTable(
                      showCheckboxColumn: false,
                      columnSpacing: 24,
                      dataRowMaxHeight: 64,
                      columns: [
                        for (final c in w.cols) DataColumn(label: Text(c.label)),
                        if (w.actions != null || w.delete != null) const DataColumn(label: Text('')),
                      ],
                      rows: [
                        for (final r in rows)
                          DataRow(
                            onSelectChanged: w.open == null ? null : (_) => _run(() => w.open!(context, r)),
                            cells: [
                              for (final c in w.cols)
                                DataCell(switch (c.value(r)) {
                                  final Widget v => v,
                                  final v => ConstrainedBox(
                                    constraints: const BoxConstraints(maxWidth: 320),
                                    child: Text(_text(v), maxLines: 2, overflow: TextOverflow.ellipsis),
                                  ),
                                }),
                              if (w.actions != null || w.delete != null)
                                DataCell(
                                  Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      ...?w.actions?.call(r, _reload),
                                      if (w.delete != null)
                                        IconButton(
                                          tooltip: 'Видалити',
                                          icon: const Icon(Icons.delete_outline),
                                          onPressed: () => _run(() async {
                                            if (!await confirm(context, 'Видалити назавжди?')) return false;
                                            await w.delete!(r);
                                            return true;
                                          }),
                                        ),
                                    ],
                                  ),
                                ),
                            ],
                          ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

Future<bool?> openEditor(BuildContext context, String title, List<F> fields, J data, Future<void> Function(J) save) =>
    Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => EditorPage(title: title, fields: fields, data: data, save: save),
      ),
    );

/// List + editor for a REST resource: GET/POST [path], GET/PUT/DELETE [path]/{id}.
Widget crud({
  required String path,
  required String noun,
  required List<Col> cols,
  required List<F> fields,
  required J Function() blank,
  bool detail = false, // list rows are trimmed → GET the full DTO before editing
}) {
  Future<bool?> edit(BuildContext context, J? row) async {
    final data = row == null
        ? blank()
        : (detail ? await api.get('$path/${row['id']}') : jsonDecode(jsonEncode(row))) as J; // deep copy
    if (!context.mounted) return false;
    return openEditor(
      context,
      row == null ? 'Новий запис: $noun' : noun,
      fields,
      data,
      (d) => row == null ? api.post(path, d) : api.put('$path/${row['id']}', d),
    );
  }

  return ListPage(
    load: () async => await api.get(path) as List,
    cols: cols,
    open: edit,
    add: (c) => edit(c, null),
    delete: (r) => api.delete('$path/${r['id']}'),
  );
}

String yes(Object? v) => v == true ? '✓' : '—';

// ── Catalog (Sales) ──

Future<List<(Object?, String)>> _categoryChoices() async => [
  for (final c in await api.get('/admin/catalog/categories') as List) (c['id'], c['nameUk'] as String),
];

Widget productsPage() => crud(
  path: '/admin/catalog/products',
  noun: 'товар',
  detail: true,
  cols: [
    Col('', (r) => thumb(r['image'])),
    Col('Назва', (r) => r['nameUk']),
    Col('Артикул', (r) => r['sku']),
    Col('Категорія', (r) => r['categoryName']),
    Col('Ціна', (r) => r['price']),
    Col('Наявність', (r) => yes(r['inStock'])),
    Col('На сайті', (r) => yes(r['published'])),
  ],
  blank: () => {'price': 0, 'inStock': true, 'published': false, 'sort': 0, 'images': [], 'files': [], 'options': []},
  fields: const [
    F('nameUk', 'Назва (UK)', required: true),
    F('nameEn', 'Назва (EN)'),
    F('slug', 'Slug (адреса /shop/...)', t: T.slug, required: true),
    F('sku', 'Артикул'),
    F('categoryId', 'Категорія', t: T.select, choices: _categoryChoices),
    F('price', 'Ціна, грн', t: T.money, required: true),
    F('oldPrice', 'Стара ціна, грн', t: T.money),
    F('inStock', 'В наявності', t: T.bool),
    F('isFeatured', 'Показувати на головній', t: T.bool),
    F('published', 'Опубліковано', t: T.bool),
    F('sort', 'Порядок', t: T.int),
    F('images', 'Фото (перше — головне)', t: T.images, folder: 'products'),
    F('options', 'Опції (комплектація, розмір…)', t: T.options),
    F('shortDescUk', 'Короткий опис (UK)', t: T.multiline),
    F('shortDescEn', 'Короткий опис (EN)', t: T.multiline),
    F('descriptionHtmlUk', 'Опис (UK)', t: T.html, folder: 'products'),
    F('descriptionHtmlEn', 'Опис (EN)', t: T.html, folder: 'products'),
    F('specsHtmlUk', 'Характеристики (UK)', t: T.html, folder: 'products'),
    F('specsHtmlEn', 'Характеристики (EN)', t: T.html, folder: 'products'),
    F('faqHtmlUk', 'FAQ (UK)', t: T.html, folder: 'products'),
    F('faqHtmlEn', 'FAQ (EN)', t: T.html, folder: 'products'),
    F('files', 'Файли для завантаження', t: T.files),
    F('seoTitleUk', 'SEO title (UK)'),
    F('seoTitleEn', 'SEO title (EN)'),
    F('seoDescUk', 'SEO description (UK)', t: T.multiline),
    F('seoDescEn', 'SEO description (EN)', t: T.multiline),
  ],
);

Widget categoriesPage() => crud(
  path: '/admin/catalog/categories',
  noun: 'категорія',
  cols: [
    Col('', (r) => thumb(r['imageUrl'])),
    Col('Назва', (r) => r['nameUk']),
    Col('Slug', (r) => r['slug']),
    Col('Порядок', (r) => r['sort']),
    Col('Товарів', (r) => r['productCount']),
  ],
  blank: () => {'sort': 0},
  fields: const [
    F('nameUk', 'Назва (UK)', required: true),
    F('nameEn', 'Назва (EN)'),
    F('slug', 'Slug (адреса /shop/cat/...)', t: T.slug, required: true),
    F('sort', 'Порядок', t: T.int),
    F('imageKey', 'Зображення', t: T.image, folder: 'categories'),
  ],
);

// ── Orders (Sales) ──

const orderStatuses = {
  'New': 'Нове',
  'AwaitingPayment': 'Очікує оплату',
  'Paid': 'Оплачено',
  'InvoiceSent': 'Рахунок надіслано',
  'Credit': 'Кредит',
  'Shipped': 'Відправлено',
  'Cancelled': 'Скасовано',
};
const _payments = {'LiqPay': 'LiqPay (картка)', 'Invoice': 'Безготівка (рахунок)', 'Credit': 'Кредит 0%'};
const _deliveries = {'NovaPoshta': 'Нова пошта', 'Ukrposhta': 'Укрпошта', 'Pickup': 'Самовивіз'};

class OrdersPage extends StatefulWidget {
  const OrdersPage({super.key});
  @override
  State<OrdersPage> createState() => _OrdersPageState();
}

class _OrdersPageState extends State<OrdersPage> {
  String? _status;

  @override
  Widget build(BuildContext context) => ListPage(
    key: ValueKey(_status),
    // ponytail: latest 200 per filter, no paging — add page buttons when that stops being enough.
    load: () async => (await api.get('/admin/orders', {'status': ?_status, 'pageSize': 200}))['items'] as List,
    header: DropdownButton<String?>(
      value: _status,
      items: [
        const DropdownMenuItem(value: null, child: Text('Усі статуси')),
        for (final e in orderStatuses.entries) DropdownMenuItem(value: e.key, child: Text(e.value)),
      ],
      onChanged: (v) => setState(() => _status = v),
    ),
    cols: [
      Col('№', (r) => r['number']),
      Col('Дата', (r) => fmtDate(r['createdAt'])),
      Col('Клієнт', (r) => r['customerName']),
      Col('Телефон', (r) => r['phone']),
      Col('Сума', (r) => r['total']),
      Col('Оплата', (r) => _payments[r['paymentMethod']]),
      Col('Статус', (r) => Chip(label: Text(orderStatuses[r['status']] ?? '${r['status']}'))),
    ],
    open: (context, o) => showDialog<bool>(context: context, builder: (_) => _OrderDialog(o)),
  );
}

class _OrderDialog extends StatefulWidget {
  const _OrderDialog(this.o);
  final J o;
  @override
  State<_OrderDialog> createState() => _OrderDialogState();
}

class _OrderDialogState extends State<_OrderDialog> {
  late String _status = widget.o['status'];

  @override
  Widget build(BuildContext context) {
    final o = widget.o;
    Widget line(String k, Object? v) => v == null || '$v'.isEmpty
        ? const SizedBox.shrink()
        : Padding(padding: const EdgeInsets.only(bottom: 4), child: SelectableText('$k: $v'));
    return AlertDialog(
      title: Text('Замовлення №${o['number']} від ${fmtDate(o['createdAt'])}'),
      content: SizedBox(
        width: 640,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              line('Клієнт', o['customerName']),
              line('Телефон', o['phone']),
              line('Email', o['email']),
              line(
                'Доставка',
                [_deliveries[o['deliveryMethod']], o['deliveryCity'], o['deliveryWarehouse']].nonNulls.join(', '),
              ),
              line('Оплата', _payments[o['paymentMethod']]),
              line('LiqPay', o['liqPayStatus']),
              line('Оплачено', o['paidAt'] == null ? null : fmtDate(o['paidAt'])),
              line('Коментар', o['comment']),
              const Divider(),
              for (final i in o['items'] as List)
                line(
                  '${i['productName']}${i['optionsText'] == null ? '' : ' (${i['optionsText']})'}',
                  '${i['qty']} × ${i['unitPrice']} = ${i['lineTotal']} грн',
                ),
              const Divider(),
              line('Товари', '${o['subtotal']} грн'),
              line('Доставка', '${o['deliveryCost']} грн'),
              Text('Разом: ${o['total']} грн', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                initialValue: _status,
                decoration: const InputDecoration(labelText: 'Статус', border: OutlineInputBorder()),
                items: [for (final e in orderStatuses.entries) DropdownMenuItem(value: e.key, child: Text(e.value))],
                onChanged: (v) => _status = v!,
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Закрити')),
        FilledButton(
          onPressed: () async {
            try {
              await api.patch('/admin/orders/${o['id']}/status', {'status': _status});
              if (context.mounted) Navigator.pop(context, true);
            } catch (e) {
              if (context.mounted) toast(context, '$e');
            }
          },
          child: const Text('Зберегти статус'),
        ),
      ],
    );
  }
}

// ── Content (Editor) ──

Widget blogPage() => crud(
  path: '/admin/blog',
  noun: 'стаття',
  cols: [
    Col('', (r) => thumb(r['coverUrl'])),
    Col('Заголовок', (r) => r['titleUk']),
    Col('Дата', (r) => fmtDate(r['publishedAt'], time: false)),
    Col('На сайті', (r) => yes(r['published'])),
  ],
  blank: () => {'published': false, 'bodyHtmlUk': ''},
  fields: const [
    F('titleUk', 'Заголовок (UK)', required: true),
    F('titleEn', 'Заголовок (EN)'),
    F('slug', 'Slug (адреса /blog/...)', t: T.slug, required: true),
    F('publishedAt', 'Дата публікації', t: T.date),
    F('author', 'Автор'),
    F('published', 'Опубліковано', t: T.bool),
    F('coverKey', 'Обкладинка', t: T.image, folder: 'blog'),
    F('excerptUk', 'Анонс (UK)', t: T.multiline),
    F('excerptEn', 'Анонс (EN)', t: T.multiline),
    F('bodyHtmlUk', 'Текст (UK)', t: T.html, required: true, folder: 'blog'),
    F('bodyHtmlEn', 'Текст (EN)', t: T.html, folder: 'blog'),
    F('seoTitleUk', 'SEO title (UK)'),
    F('seoTitleEn', 'SEO title (EN)'),
    F('seoDescUk', 'SEO description (UK)', t: T.multiline),
    F('seoDescEn', 'SEO description (EN)', t: T.multiline),
  ],
);

Widget contentPage() {
  const body = [F('title', 'Заголовок'), F('bodyHtml', 'Текст', t: T.html, folder: 'content')];
  Future<void> save(J d) =>
      api.put('/admin/content/${d['key']}/${d['lang']}', {'title': d['title'], 'bodyHtml': d['bodyHtml'] ?? ''});
  return ListPage(
    load: () async => await api.get('/admin/content') as List,
    cols: [Col('Ключ', (r) => r['key']), Col('Мова', (r) => r['lang']), Col('Заголовок', (r) => r['title'])],
    open: (c, r) => openEditor(c, '${r['key']} (${r['lang']})', body, J.of(r), save),
    add: (c) => openEditor(
      c,
      'Новий блок',
      [
        const F('key', 'Ключ (напр. page.about)', required: true),
        F('lang', 'Мова', t: T.select, required: true, choices: () async => [('uk', 'uk'), ('en', 'en')]),
        ...body,
      ],
      {},
      save,
    ),
    delete: (r) => api.delete('/admin/content/${r['key']}/${r['lang']}'),
  );
}

/// Key/value site settings (phones, address, socials, analytics ids).
class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});
  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  final _rows = <(TextEditingController, TextEditingController)>[];
  Set<String> _loaded = {};
  Object? _error;
  bool _busy = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _busy = true);
    try {
      _show(await api.get('/admin/settings') as J);
    } catch (e) {
      _error = e;
    }
    if (mounted) setState(() => _busy = false);
  }

  void _show(J m) {
    _rows
      ..clear()
      ..addAll([
        for (final e in m.entries) (TextEditingController(text: e.key), TextEditingController(text: '${e.value}')),
      ]);
    _loaded = m.keys.toSet();
  }

  Future<void> _save() async {
    final m = <String, String?>{for (final k in _loaded) k: null}; // removed rows → delete
    for (final (k, v) in _rows) {
      if (k.text.trim().isNotEmpty) m[k.text.trim()] = v.text;
    }
    setState(() => _busy = true);
    try {
      _show(await api.put('/admin/settings', m) as J);
      if (mounted) toast(context, 'Збережено');
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) return Center(child: Text('$_error'));
    if (_busy && _rows.isEmpty) return const Center(child: CircularProgressIndicator());
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        for (final (i, (k, v)) in _rows.indexed)
          Padding(
            key: ObjectKey(k),
            padding: const EdgeInsets.only(bottom: 8),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 240,
                  child: TextField(
                    controller: k,
                    decoration: const InputDecoration(labelText: 'Ключ', border: OutlineInputBorder()),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextField(
                    controller: v,
                    minLines: 1,
                    maxLines: 4,
                    decoration: const InputDecoration(labelText: 'Значення', border: OutlineInputBorder()),
                  ),
                ),
                IconButton(icon: const Icon(Icons.delete_outline), onPressed: () => setState(() => _rows.removeAt(i))),
              ],
            ),
          ),
        Wrap(
          spacing: 12,
          children: [
            OutlinedButton.icon(
              icon: const Icon(Icons.add),
              label: const Text('Додати'),
              onPressed: () => setState(() => _rows.add((TextEditingController(), TextEditingController()))),
            ),
            FilledButton.icon(
              onPressed: _busy ? null : _save,
              icon: const Icon(Icons.save),
              label: const Text('Зберегти'),
            ),
          ],
        ),
      ],
    );
  }
}

// ── Inbox ──

/// Full text in a dialog; opening marks the row read.
Future<bool?> _showMessage(BuildContext context, String path, J r, List<(String, Object?)> lines) async {
  if (r['isRead'] != true) await api.patch('$path/${r['id']}', {'isRead': true});
  if (!context.mounted) return true;
  await showDialog<void>(
    context: context,
    builder: (c) => AlertDialog(
      title: Text('${r['name']} · ${fmtDate(r['createdAt'])}'),
      content: SizedBox(
        width: 560,
        child: SingleChildScrollView(
          child: SelectableText(
            [
              for (final (k, v) in lines)
                if (v != null && '$v'.isNotEmpty) '$k: $v',
            ].join('\n\n'),
          ),
        ),
      ),
      actions: [TextButton(onPressed: () => Navigator.pop(c), child: const Text('Закрити'))],
    ),
  );
  return true;
}

List<Widget> _readToggle(String path, J r, VoidCallback reload) => [
  IconButton(
    tooltip: r['isRead'] == true ? 'Позначити непрочитаним' : 'Позначити прочитаним',
    icon: Icon(r['isRead'] == true ? Icons.mark_email_unread_outlined : Icons.mark_email_read_outlined),
    onPressed: () async {
      await api.patch('$path/${r['id']}', {'isRead': r['isRead'] != true});
      reload();
    },
  ),
];

Col _unread() => Col('', (r) => r['isRead'] == true ? null : const Icon(Icons.circle, size: 10, color: Colors.green));

Widget applicationsPage() {
  const path = '/admin/applications';
  return ListPage(
    load: () async => await api.get(path) as List,
    cols: [
      _unread(),
      Col('Дата', (r) => fmtDate(r['createdAt'])),
      Col('Вакансія', (r) => r['vacancyTitle'] ?? '— (загальний)'),
      Col("Ім'я", (r) => r['name']),
      Col('Телефон', (r) => r['phone']),
      Col('Email', (r) => r['email']),
    ],
    open: (c, r) => _showMessage(c, path, r, [
      ('Вакансія', r['vacancyTitle']),
      ('Телефон', r['phone']),
      ('Email', r['email']),
      ('Повідомлення', r['message']),
    ]),
    actions: (r, reload) => [
      if (r['hasCv'] == true)
        Builder(
          builder: (context) => IconButton(
            tooltip: 'Завантажити CV',
            icon: const Icon(Icons.download),
            onPressed: () async {
              try {
                browser.saveBytes(await api.bytes('$path/${r['id']}/cv'), r['cvFileName'] ?? 'cv');
              } catch (e) {
                if (context.mounted) toast(context, '$e');
              }
            },
          ),
        ),
      ..._readToggle(path, r, reload),
    ],
    delete: (r) => api.delete('$path/${r['id']}'),
  );
}

Widget contactsPage() {
  const path = '/admin/contact-requests';
  return ListPage(
    load: () async => await api.get(path) as List,
    cols: [
      _unread(),
      Col('Дата', (r) => fmtDate(r['createdAt'])),
      Col("Ім'я", (r) => r['name']),
      Col('Телефон', (r) => r['phone']),
      Col('Email', (r) => r['email']),
      Col('Повідомлення', (r) => r['message']),
    ],
    open: (c, r) =>
        _showMessage(c, path, r, [('Телефон', r['phone']), ('Email', r['email']), ('Повідомлення', r['message'])]),
    actions: (r, reload) => _readToggle(path, r, reload),
    delete: (r) => api.delete('$path/${r['id']}'),
  );
}

// ── HR ──

Widget vacanciesPage() => crud(
  path: '/admin/vacancies',
  noun: 'вакансія',
  cols: [
    Col('Назва', (r) => r['titleUk']),
    Col('Відділ', (r) => r['department']),
    Col('Місто', (r) => r['city']),
    Col('Активна', (r) => yes(r['isActive'])),
    Col('Відгуків', (r) => r['applications']),
  ],
  blank: () => {'isActive': true, 'sort': 0, 'city': 'Львів', 'department': '', 'employmentType': ''},
  fields: const [
    F('titleUk', 'Назва (UK)', required: true),
    F('titleEn', 'Назва (EN)'),
    F('slug', 'Slug (адреса /vacancies/...)', t: T.slug, required: true),
    F('department', 'Відділ'),
    F('employmentType', 'Зайнятість'),
    F('city', 'Місто'),
    F('experience', 'Досвід'),
    F('language', 'Мова'),
    F('isActive', 'Активна (видно на сайті)', t: T.bool),
    F('sort', 'Порядок', t: T.int),
    F('publishedAt', 'Дата публікації', t: T.date),
    F('requirementsHtmlUk', 'Вимоги (UK)', t: T.html),
    F('requirementsHtmlEn', 'Вимоги (EN)', t: T.html),
    F('niceToHaveHtmlUk', 'Буде плюсом (UK)', t: T.html),
    F('niceToHaveHtmlEn', 'Буде плюсом (EN)', t: T.html),
    F('conditionsHtmlUk', 'Умови (UK)', t: T.html),
    F('conditionsHtmlEn', 'Умови (EN)', t: T.html),
  ],
);

// ── Users (Admin) ──

const siteRoles = {'Admin': 'Адмін', 'Sales': 'Продажі', 'Editor': 'Редактор', 'Hr': 'HR'};

class UsersPage extends StatefulWidget {
  const UsersPage({super.key});
  @override
  State<UsersPage> createState() => _UsersPageState();
}

class _UsersPageState extends State<UsersPage> {
  final _gospodarDown = ValueNotifier(false);

  @override
  Widget build(BuildContext context) => ListPage(
    load: () async {
      final res = await api.get('/admin/users') as J;
      _gospodarDown.value = res['gospodarAvailable'] != true;
      return res['users'] as List;
    },
    header: ValueListenableBuilder(
      valueListenable: _gospodarDown,
      builder: (_, down, _) => down
          ? const Text('ERP недоступна — показано лише користувачів із ролями', style: TextStyle(color: Colors.red))
          : const Text('Користувачі ERP; роль дає доступ до адмінки'),
    ),
    cols: [Col("Ім'я", (r) => r['fullName']), Col('Email', (r) => r['email'])],
    actions: (r, reload) => r['gospodarAdmin'] == true
        ? [const Chip(label: Text('Адмін ERP — повний доступ'))]
        : [
            for (final e in siteRoles.entries)
              Builder(
                builder: (context) => Padding(
                  padding: const EdgeInsets.only(right: 4),
                  child: FilterChip(
                    label: Text(e.value),
                    selected: (r['roles'] as List).contains(e.key),
                    onSelected: (on) async {
                      final roles = [...r['roles'] as List]..remove(e.key);
                      if (on) roles.add(e.key);
                      try {
                        await api.put('/admin/users/${r['id']}/roles', {
                          'roles': roles,
                          'email': r['email'],
                          'fullName': r['fullName'],
                        });
                        reload();
                      } catch (err) {
                        if (context.mounted) toast(context, '$err');
                      }
                    },
                  ),
                ),
              ),
          ],
  );
}
