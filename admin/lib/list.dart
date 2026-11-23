import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';

/// One page from the server: `{items, total}` (the API's `Page<T>`).
typedef PageLoader = Future<(List<J>, int)> Function(int page, int pageSize, String q);

/// `GET path?page&pageSize&q&...query` → a [PageLoader].
PageLoader pagedGet(String path, [Map<String, Object?> query = const {}]) => (page, size, q) async {
  final r = await api.get(path, {
    for (final e in query.entries)
      if (e.value != null) e.key: e.value,
    'page': page,
    'pageSize': size,
    if (q.isNotEmpty) 'q': q,
  }) as J;
  return ((r['items'] as List).cast<J>(), (r['total'] as num).toInt());
};

/// Search + filters toolbar over an endlessly scrolling list: the next page loads
/// ~600 px before the end. Change [query] (filters) → starts over from page 1.
class PagedList extends StatefulWidget {
  const PagedList({
    super.key,
    required this.load,
    required this.item,
    this.query = const {},
    this.searchHint = 'Пошук',
    this.filters = const [],
    this.actions = const [],
    this.emptyIcon = Icons.inbox_outlined,
    this.emptyText = 'Нічого не знайдено',
    this.pageSize = 30,
    this.initialQuery = '',
  });

  final PageLoader load;
  final Widget Function(BuildContext context, J row, PagedListState list) item;

  /// Anything that should restart the list when it changes (filter values).
  final Map<String, Object?> query;
  final String searchHint, emptyText, initialQuery;
  final List<Widget> filters, actions;
  final IconData emptyIcon;
  final int pageSize;

  @override
  State<PagedList> createState() => PagedListState();
}

class PagedListState extends State<PagedList> {
  final _scroll = ScrollController();
  final items = <J>[];
  int _total = 0, _page = 0, _gen = 0;
  bool _loading = false;
  String? _error;
  late String _q = widget.initialQuery;

  bool get _done => _page > 0 && items.length >= _total;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_maybeMore);
    reload();
  }

  @override
  void didUpdateWidget(PagedList oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (jsonEncode(oldWidget.query) != jsonEncode(widget.query)) reload();
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  /// Back to page 1 (after a filter change, an add or a delete).
  Future<void> reload() async {
    _gen++;
    setState(() {
      items.clear();
      _page = 0;
      _total = 0;
      _error = null;
      _loading = false;
    });
    if (_scroll.hasClients) _scroll.jumpTo(0);
    await _next();
  }

  void _maybeMore() {
    if (_scroll.hasClients && _scroll.position.extentAfter < 600) _next();
  }

  Future<void> _next() async {
    if (_loading || _done || (_error != null && items.isNotEmpty)) return;
    final gen = _gen;
    setState(() => _loading = true);
    try {
      final (rows, total) = await widget.load(_page + 1, widget.pageSize, _q);
      if (!mounted || gen != _gen) return; // a newer search/filter won
      setState(() {
        items.addAll(rows);
        _total = total;
        _page++;
        _loading = false;
      });
      // A short first page leaves nothing to scroll — keep filling the viewport.
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && _scroll.hasClients && _scroll.position.maxScrollExtent == 0) _next();
      });
    } catch (e) {
      if (!mounted || gen != _gen) return;
      setState(() {
        _error = '$e';
        _loading = false;
      });
    }
  }

  void _retry() {
    setState(() => _error = null);
    _next();
  }

  /// In-place update after a quick edit (toggle, status) — keeps the scroll position.
  void replace(J row) {
    final i = items.indexWhere((x) => x['id'] == row['id']);
    if (i >= 0) setState(() => items[i] = row);
  }

  void removeWhere(bool Function(J row) test) => setState(() {
    final before = items.length;
    items.removeWhere(test);
    _total -= before - items.length;
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        AppToolbar(
          search: AppSearchField(
            hintText: widget.searchHint,
            initialValue: widget.initialQuery,
            onChanged: (v) {
              _q = v.trim();
              reload();
            },
          ),
          filters: widget.filters,
          actions: [
            IconButton(tooltip: 'Оновити', icon: const Icon(Icons.refresh), onPressed: reload),
            ...widget.actions,
          ],
        ),
        if (_page > 0)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xl),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(_count(_total), style: theme.textTheme.bodySmall?.copyWith(color: context.palette.textTertiary)),
            ),
          ),
        Expanded(
          child: AppAsyncBody(
            isLoading: _loading,
            error: _error,
            onRetry: _retry,
            isEmpty: items.isEmpty,
            empty: AppEmptyView(icon: widget.emptyIcon, message: widget.emptyText),
            onRefresh: reload,
            child: CenteredScrollColumn(
              maxWidth: 1200,
              padding: AppSpacing.screenPadding,
              builder: (padding) => ListView.builder(
                controller: _scroll,
                physics: const AlwaysScrollableScrollPhysics(),
                padding: padding,
                itemCount: items.length + 1,
                itemBuilder: (context, i) => i < items.length ? widget.item(context, items[i], this) : _footer(context),
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _footer(BuildContext context) => Padding(
    padding: AppSpacing.paddingLg,
    child: Center(
      child: _error != null
          ? TextButton.icon(onPressed: _retry, icon: const Icon(Icons.refresh), label: Text('Не вдалося підвантажити: $_error'))
          : _loading
          ? const SizedBox.square(dimension: AppSizes.progressIndicatorMd, child: CircularProgressIndicator(strokeWidth: 2))
          : items.length > widget.pageSize
          ? Text('Це всі записи', style: TextStyle(color: context.palette.textFaint))
          : const SizedBox.shrink(),
    ),
  );
}

String _count(int n) {
  final m10 = n % 10, m100 = n % 100;
  final w = m10 == 1 && m100 != 11 ? 'запис' : (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) ? 'записи' : 'записів';
  return '$n $w';
}

/// Run an action from a list row/dialog with standard error feedback; true = done.
Future<bool> attempt(BuildContext context, Future<Object?> Function() fn, {String? ok}) async {
  try {
    await fn();
    if (ok != null && context.mounted) AppFeedback.showSuccess(context, ok);
    return true;
  } catch (e) {
    if (context.mounted) AppFeedback.showError(context, '$e');
    return false;
  }
}

/// Filter chip in the toolbar from a `{value: label}` map; null = all.
Widget filterChip<V>({
  required IconData icon,
  required String tooltip,
  required String allLabel,
  required Map<V, String> options,
  required V? value,
  required ValueChanged<V?> onSelected,
}) => FilterMenuChip<V?>(
  icon: icon,
  tooltip: tooltip,
  value: value == null ? allLabel : options[value] ?? '$value',
  active: value != null,
  entries: [(value: null, label: allLabel), for (final e in options.entries) (value: e.key, label: e.value)],
  selected: value,
  onSelected: onSelected,
);

Color tone(BuildContext context, String name) {
  final s = context.semantic, c = Theme.of(context).colorScheme;
  return switch (name) {
    'success' => s.success,
    'warning' => s.warning,
    'info' => s.info,
    'error' => c.error,
    _ => context.palette.textTertiary,
  };
}

Widget chip(BuildContext context, String label, String toneName, {IconData? icon}) =>
    StatusChip(label: label, color: tone(context, toneName), icon: icon);

/// Gospodar CRM link state of an order / contact request.
Widget crmChip(BuildContext context, J? crm) {
  if (crm == null) return const SizedBox.shrink();
  if (crm['dealId'] != null) return chip(context, 'У CRM', 'success', icon: Icons.link);
  if (crm['error'] != null) {
    return Tooltip(message: '${crm['error']}', child: chip(context, 'CRM: помилка', 'error', icon: Icons.link_off));
  }
  return const SizedBox.shrink();
}

/// Card body for CRM state with "open deal" / "send again".
class CrmPanel extends StatelessWidget {
  const CrmPanel({super.key, required this.crm, required this.onPush});
  final J crm;
  final Future<void> Function() onPush;

  @override
  Widget build(BuildContext context) {
    final erp = summary.link('erp');
    final deal = crm['dealId'];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (deal != null) ...[
          InfoRow(label: 'Угода', valueWidget: chip(context, 'Створено ${fmtDate(crm['syncedAt'])}', 'success', icon: Icons.link)),
          if (erp != null)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                icon: const Icon(Icons.open_in_new),
                label: const Text('Відкрити угоду в Господарі'),
                onPressed: () => browser.openTab('$erp/crm/deals/$deal'),
              ),
            ),
        ] else ...[
          if (crm['error'] != null) ...[FormBanner('${crm['error']}'), AppSpacing.gapSm],
          InfoRow(
            label: 'Стан',
            value: (crm['attempts'] as num? ?? 0) == 0 ? 'Ще не надсилалось' : 'Спроб: ${crm['attempts']}',
          ),
          AppSpacing.gapSm,
          Align(
            alignment: Alignment.centerLeft,
            child: _PushButton(onPush: onPush, retry: (crm['attempts'] as num? ?? 0) > 0),
          ),
        ],
      ],
    );
  }
}

class _PushButton extends StatefulWidget {
  const _PushButton({required this.onPush, required this.retry});
  final Future<void> Function() onPush;
  final bool retry;
  @override
  State<_PushButton> createState() => _PushButtonState();
}

class _PushButtonState extends State<_PushButton> {
  bool _busy = false;
  @override
  Widget build(BuildContext context) => AppButton(
    label: widget.retry ? 'Надіслати в CRM ще раз' : 'Надіслати в CRM',
    icon: Icons.send_outlined,
    variant: AppButtonVariant.secondary,
    isLoading: _busy,
    onPressed: () async {
      setState(() => _busy = true);
      await widget.onPush();
      if (mounted) setState(() => _busy = false);
    },
  );
}

Future<bool?> openEditor(
  BuildContext context,
  String title,
  List<F> fields,
  J data,
  Future<void> Function(J) save, {
  String? subtitle,
  List<Widget Function(BuildContext)> actions = const [],
}) => Navigator.of(context).push<bool>(
  MaterialPageRoute(
    builder: (_) => EditorPage(title: title, subtitle: subtitle, fields: fields, data: data, save: save, actions: actions),
  ),
);

J deepCopy(J m) => jsonDecode(jsonEncode(m)) as J;

/// Editor for one REST resource row: GET (detail) / POST / PUT, plus the editor's
/// "open on site" and "delete" actions. A slug change on a published row asks
/// first — the API then adds a 301 from the old address.
Future<bool?> editResource(
  BuildContext context, {
  required String path,
  required String noun,
  required List<F> fields,
  required J? row,
  required J Function() blank,
  bool detail = false,
  String Function(J row)? title,
  String? Function(J row)? sitePath,
  String Function(J row)? deleteWarning,
  bool Function(J row)? published,
  List<Widget Function(BuildContext, J row)> extraActions = const [],
}) async {
  final data = row == null ? blank() : (detail ? await api.get('$path/${row['id']}') as J : deepCopy(row));
  if (!context.mounted) return false;
  final oldSlug = data['slug'];
  final site = summary.link('site');
  return openEditor(
    context,
    row == null ? 'Новий запис: $noun' : (title?.call(data) ?? noun),
    subtitle: data['updatedAt'] == null ? null : 'Змінено ${fmtDate(data['updatedAt'])}',
    fields,
    data,
    (d) async {
      if (row != null && oldSlug != null && d['slug'] != oldSlug && (published?.call(d) ?? true)) {
        final from = sitePath?.call({...d, 'slug': oldSlug}) ?? oldSlug;
        final to = sitePath?.call(d) ?? d['slug'];
        if (!context.mounted ||
            !await showConfirmDialog(
              context,
              title: 'Змінити адресу сторінки?',
              message: '$from → $to\n\nСтара адреса автоматично переадресовуватиме на нову (301), '
                  'але посилання в рекламі та соцмережах краще оновити.',
              confirmLabel: 'Змінити адресу',
              destructive: false,
            )) {
          throw const SaveCancelled();
        }
      }
      row == null ? await api.post(path, d) : await api.put('$path/${row['id']}', d);
    },
    actions: [
      if (row != null && site != null && sitePath?.call(data) != null)
        (c) => IconButton(
          tooltip: 'Відкрити на сайті',
          icon: const Icon(Icons.open_in_new),
          onPressed: () => browser.openTab('$site${sitePath!(data)}'),
        ),
      if (row != null) ...extraActions.map((a) => (BuildContext c) => a(c, data)),
      if (row != null)
        (c) => IconButton(
          tooltip: 'Видалити',
          icon: Icon(Icons.delete_outline, color: Theme.of(c).colorScheme.error),
          onPressed: () async {
            if (!await showConfirmDialog(
                  c,
                  title: 'Видалити «${title?.call(data) ?? noun}»?',
                  message: '${deleteWarning?.call(data) ?? ''}Цю дію не можна скасувати.'.trim(),
                ) ||
                !c.mounted) {
              return;
            }
            final ok = await attempt(c, () => api.delete('$path/${row['id']}'), ok: 'Видалено');
            if (ok && c.mounted) Navigator.pop(c, true);
          },
        ),
    ],
  );
}

/// Row-level delete with the same wording as the editor's.
Future<bool> confirmDelete(BuildContext context, String name, String path, {String warning = ''}) async {
  if (!await showConfirmDialog(context, title: 'Видалити «$name»?', message: '$warningЦю дію не можна скасувати.') ||
      !context.mounted) {
    return false;
  }
  return attempt(context, () => api.delete(path), ok: 'Видалено');
}
