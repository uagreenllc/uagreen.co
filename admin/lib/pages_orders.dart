import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'form.dart';
import 'list.dart';
import 'main.dart' show nav;

const _orders = '/admin/orders';

const orderStatuses = {
  'New': 'Нове',
  'AwaitingPayment': 'Очікує оплату',
  'Paid': 'Оплачено',
  'InvoiceSent': 'Рахунок надіслано',
  'Credit': 'Кредит',
  'Shipped': 'Відправлено',
  'Cancelled': 'Скасовано',
};
const _statusTone = {
  'New': 'info',
  'AwaitingPayment': 'warning',
  'Paid': 'success',
  'InvoiceSent': 'warning',
  'Credit': 'warning',
  'Shipped': 'success',
  'Cancelled': 'neutral',
};
const payments = {'LiqPay': 'LiqPay (картка)', 'Invoice': 'Вручну (переказ на IBAN)', 'Credit': 'Кредит 0%'};
const deliveries = {'NovaPoshta': 'Нова пошта', 'Ukrposhta': 'Укрпошта', 'Pickup': 'Самовивіз'};

Widget orderStatusChip(BuildContext context, String? s) =>
    chip(context, orderStatuses[s] ?? '$s', _statusTone[s] ?? 'neutral');

class OrdersPage extends StatefulWidget {
  const OrdersPage({super.key});
  @override
  State<OrdersPage> createState() => _OrdersPageState();
}

class _OrdersPageState extends State<OrdersPage> {
  final _list = GlobalKey<PagedListState>();
  String? _status = nav.take('status'), _crm = nav.take('crm');

  Future<void> _open(J r) async {
    final changed = await Navigator.of(context).push<J>(MaterialPageRoute(builder: (_) => OrderPage(id: r['id'])));
    if (changed != null) {
      _list.currentState?.replace(changed);
      summary.refresh();
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet(_orders, {'status': _status, 'crm': _crm}),
    query: {'status': _status, 'crm': _crm},
    searchHint: '№, ім\'я, телефон або email',
    emptyIcon: Icons.receipt_long_outlined,
    emptyText: 'Замовлень не знайдено',
    filters: [
      filterChip(
        icon: Icons.flag_outlined,
        tooltip: 'Статус',
        allLabel: 'Усі статуси',
        options: orderStatuses,
        value: _status,
        onSelected: (v) => setState(() => _status = v),
      ),
      filterChip(
        icon: Icons.link,
        tooltip: 'CRM',
        allLabel: 'CRM: усі',
        options: const {'failed': 'CRM: помилка', 'pending': 'CRM: не надіслано'},
        value: _crm,
        onSelected: (v) => setState(() => _crm = v),
      ),
    ],
    item: (context, r, _) {
      final theme = Theme.of(context);
      final items = (r['items'] as List).cast<J>();
      return AppListCard(
        leading: CircleAvatar(
          backgroundColor: context.palette.chipBg,
          child: Text('${r['number']}', style: theme.textTheme.labelMedium?.numeric),
        ),
        title: Text('${r['customerName']} · ${r['phone']}', maxLines: 1, overflow: TextOverflow.ellipsis),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              items.map((i) => '${i['productName']}${i['qty'] == 1 ? '' : ' ×${i['qty']}'}').join(', '),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            AppSpacing.gapXs,
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.xs,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Text('${fmtDate(r['createdAt'])} · ${payments[r['paymentMethod']] ?? ''}'),
                orderStatusChip(context, r['status']),
                crmChip(context, r['crm']),
                if ((r['adminNote'] as String?)?.isNotEmpty == true)
                  const Tooltip(message: 'Є нотатка менеджера', child: Icon(Icons.sticky_note_2_outlined, size: AppSizes.iconSm)),
              ],
            ),
          ],
        ),
        isThreeLine: true,
        trailing: Text(money(r['total']), style: theme.textTheme.titleMedium?.numeric),
        onTap: () => _open(r),
      );
    },
  );
}

/// One order: status (applied at once, risky ones confirmed), editable contacts,
/// delivery, items and note (saved together), CRM and the change history.
class OrderPage extends StatefulWidget {
  const OrderPage({super.key, required this.id});
  final String id;
  @override
  State<OrderPage> createState() => _OrderPageState();
}

class _OrderPageState extends State<OrderPage> with UnsavedChangesGuard {
  final _form = GlobalKey<FormState>();
  J? _o, _edit;
  String _initial = '';
  String? _error, _saveError;
  bool _busy = false;

  @override
  bool get isDirty => _edit != null && jsonEncode(_edit) != _initial;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      _show(await api.get('$_orders/${widget.id}') as J);
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    }
  }

  void _show(J o) {
    if (!mounted) return;
    setState(() {
      _o = o;
      _edit = {
        for (final k in ['customerName', 'phone', 'email', 'deliveryMethod', 'deliveryCity', 'deliveryWarehouse', 'comment', 'adminNote', 'deliveryCost'])
          k: o[k],
        'items': [
          for (final i in o['items'] as List) {'id': i['id'], 'qty': i['qty'], 'unitPrice': i['unitPrice']},
        ],
      };
      _initial = jsonEncode(_edit);
      _formKeyGen++;
    });
  }

  int _formKeyGen = 0; // rebuilds the fields with server values after a save

  num get _subtotal => [
    for (final i in _edit!['items'] as List) (i['qty'] as num) * (i['unitPrice'] as num),
  ].fold<num>(0, (a, b) => a + b);

  Future<void> _setStatus(String st) async {
    final o = _o!;
    if (st == o['status']) return;
    if (st == 'Cancelled' &&
        !await showConfirmDialog(context,
            title: 'Скасувати замовлення №${o['number']}?',
            message: o['paidAt'] != null
                ? 'Замовлення вже оплачене — гроші автоматично НЕ повертаються, повернення робиться в LiqPay/банку окремо.'
                : 'Клієнт побачить статус «Скасовано» на сторінці замовлення.',
            confirmLabel: 'Скасувати замовлення')) {
      return;
    }
    if (st == 'Paid' &&
        o['paidAt'] == null &&
        mounted &&
        !await showConfirmDialog(context,
            title: 'Позначити оплаченим вручну?',
            message: 'LiqPay не підтверджував оплату. Робіть це, лише якщо гроші надійшли на рахунок.',
            confirmLabel: 'Так, гроші надійшли',
            destructive: false)) {
      return;
    }
    if (!mounted) return;
    try {
      // Only the card: unsaved edits in the form below must survive a status change.
      final r = await api.patch('$_orders/${widget.id}/status', {'status': st}) as J;
      if (!mounted) return;
      setState(() => _o = r);
      AppFeedback.showSuccess(context, 'Статус: ${orderStatuses[st]}');
    } catch (e) {
      if (mounted) AppFeedback.showError(context, '$e');
    }
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    final o = _o!;
    if ((o['paidAt'] != null || o['liqPayStatus'] != null) &&
        _subtotal + (_edit!['deliveryCost'] as num? ?? 0) != o['total'] &&
        !await showConfirmDialog(context,
            title: 'Змінити суму оплаченого замовлення?',
            message: 'Було ${money(o['total'])}, стане ${money(_subtotal + (_edit!['deliveryCost'] as num? ?? 0))}. '
                'Різницю з клієнтом треба врегулювати окремо (доплата або повернення).',
            confirmLabel: 'Змінити суму',
            destructive: false)) {
      return;
    }
    setState(() {
      _busy = true;
      _saveError = null;
    });
    try {
      _show(await api.put('$_orders/${widget.id}', _edit) as J);
      if (mounted) AppFeedback.showSuccess(context, 'Збережено');
    } catch (e) {
      if (mounted) setState(() => _saveError = '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final o = _o;
    return guard(
      child: SaveShortcut(
        onSave: _save,
        child: Focus(
          autofocus: true,
          child: Scaffold(
            appBar: AppBar(
              leading: BackButton(onPressed: () async {
                if (await confirmClose() && context.mounted) Navigator.pop(context, _o);
              }),
              title: Text(o == null ? 'Замовлення' : 'Замовлення №${o['number']} від ${fmtDate(o['createdAt'])}'),
              actions: [
                if (o != null)
                  AppButton(label: 'Зберегти', icon: Icons.save_outlined, isLoading: _busy, onPressed: isDirty ? _save : null),
                AppSpacing.hGapLg,
              ],
            ),
            body: AppAsyncBody(
              isLoading: o == null && _error == null,
              error: _error,
              onRetry: _load,
              isEmpty: o == null,
              child: o == null ? const SizedBox.shrink() : _body(context, o),
            ),
          ),
        ),
      ),
    );
  }

  Widget _body(BuildContext context, J o) {
    final e = _edit!;
    final theme = Theme.of(context);
    final wide = MediaQuery.sizeOf(context).width >= Breakpoints.tablet + 200;
    void changed() => setState(() {});

    final status = AppSectionCard(
      title: 'Статус',
      icon: Icons.flag_outlined,
      child: Wrap(
        spacing: AppSpacing.sm,
        runSpacing: AppSpacing.sm,
        children: [
          for (final s in orderStatuses.entries)
            ChoiceChip(label: Text(s.value), selected: o['status'] == s.key, onSelected: (_) => _setStatus(s.key)),
        ],
      ),
    );

    final customer = AppSectionCard(
      title: 'Клієнт',
      icon: Icons.person_outline,
      child: Column(
        children: [
          textField(e, 'customerName', "Ім'я", required: true),
          AppSpacing.gapMd,
          Row(
            children: [
              Expanded(child: textField(e, 'phone', 'Телефон', required: true)),
              IconButton(tooltip: 'Копіювати', icon: const Icon(Icons.copy, size: AppSizes.iconSm),
                  onPressed: () => copyToClipboard(context, '${e['phone']}')),
            ],
          ),
          AppSpacing.gapMd,
          textField(e, 'email', 'Email'),
          AppSpacing.gapMd,
          textField(e, 'comment', 'Коментар клієнта', lines: 3),
        ],
      ),
    );

    final delivery = AppSectionCard(
      title: 'Доставка і оплата',
      icon: Icons.local_shipping_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          DropdownButtonFormField<String>(
            initialValue: e['deliveryMethod'],
            decoration: const InputDecoration(labelText: 'Спосіб доставки'),
            items: [for (final d in deliveries.entries) DropdownMenuItem(value: d.key, child: Text(d.value))],
            onChanged: (v) => setState(() => e['deliveryMethod'] = v),
          ),
          AppSpacing.gapMd,
          textField(e, 'deliveryCity', 'Місто'),
          AppSpacing.gapMd,
          textField(e, 'deliveryWarehouse', 'Відділення / адреса'),
          AppSpacing.gapMd,
          InfoRow(label: 'Оплата', value: payments[o['paymentMethod']] ?? '${o['paymentMethod']}'),
          if (o['liqPayStatus'] != null) InfoRow(label: 'LiqPay', value: '${o['liqPayStatus']}'),
          InfoRow(label: 'Оплачено', value: o['paidAt'] == null ? '—' : fmtDate(o['paidAt'])),
        ],
      ),
    );

    final items = (o['items'] as List).cast<J>();
    final edits = (e['items'] as List).cast<J>();
    final lines = AppSectionCard(
      title: 'Позиції',
      icon: Icons.shopping_cart_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final x in edits)
            Builder(builder: (context) {
              final src = items.firstWhere((i) => i['id'] == x['id']);
              return Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.md),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${src['productName']}', style: theme.textTheme.bodyLarge),
                          if (src['optionsText'] != null) Text('${src['optionsText']}', style: theme.textTheme.bodySmall),
                        ],
                      ),
                    ),
                    SizedBox(
                      width: 72,
                      child: textField(x, 'qty', 'К-сть', number: (s) => int.tryParse(s.trim()), empty: 1),
                    ),
                    AppSpacing.hGapSm,
                    SizedBox(
                      width: 130,
                      child: textField(x, 'unitPrice', 'Ціна', number: (s) => num.tryParse(s.replaceAll(',', '.')), empty: 0),
                    ),
                    IconButton(
                      tooltip: 'Прибрати позицію',
                      icon: const Icon(Icons.delete_outline),
                      onPressed: edits.length == 1
                          ? null
                          : () async {
                              if (await showConfirmDialog(context,
                                  message: 'Прибрати «${src['productName']}» із замовлення? Зміниться сума.',
                                  confirmLabel: 'Прибрати')) {
                                setState(() => edits.remove(x));
                              }
                            },
                    ),
                  ],
                ),
              );
            }),
          const Divider(),
          Row(
            children: [
              const Expanded(child: Text('Доставка')),
              SizedBox(
                width: 130,
                child: textField(e, 'deliveryCost', 'Вартість', number: (s) => num.tryParse(s.replaceAll(',', '.')), empty: 0),
              ),
              const SizedBox(width: 48),
            ],
          ),
          AppSpacing.gapMd,
          Listener(
            onPointerUp: (_) => changed(),
            child: Row(
              children: [
                Expanded(child: Text('Разом', style: theme.textTheme.titleMedium)),
                Text(money(_subtotal + (e['deliveryCost'] as num? ?? 0)), style: theme.textTheme.titleLarge?.numeric),
              ],
            ),
          ),
          if (isDirty && _subtotal + (e['deliveryCost'] as num? ?? 0) != o['total'])
            Text('Було ${money(o['total'])}', textAlign: TextAlign.end, style: theme.textTheme.bodySmall),
        ],
      ),
    );

    final note = AppSectionCard(
      title: 'Нотатка менеджера',
      subtitle: 'Клієнт її не бачить',
      icon: Icons.sticky_note_2_outlined,
      child: textField(e, 'adminNote', 'Нотатка', lines: 5),
    );

    final crm = AppSectionCard(
      title: 'Господар (CRM)',
      icon: Icons.business_center_outlined,
      child: CrmPanel(
        crm: (o['crm'] as J?) ?? {},
        onPush: () async {
          try {
            _show(await api.post('$_orders/${widget.id}/crm', null) as J);
            final err = _o!['crm']?['error'];
            if (!mounted) return;
            err == null ? AppFeedback.showSuccess(this.context, 'Угоду створено') : AppFeedback.showError(this.context, '$err');
          } catch (e) {
            if (mounted) AppFeedback.showError(this.context, '$e');
          }
        },
      ),
    );

    final events = (o['events'] as List? ?? const []).cast<J>();
    final history = AppSectionCard(
      title: 'Історія',
      icon: Icons.history,
      child: events.isEmpty
          ? Text('Змін ще не було. Оформлено ${fmtDate(o['createdAt'])}.', style: theme.textTheme.bodySmall)
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (final ev in events)
                  Padding(
                    padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('${fmtDate(ev['at'])} · ${ev['actor']}', style: theme.textTheme.labelSmall),
                        Text('${ev['text']}'),
                      ],
                    ),
                  ),
              ],
            ),
    );

    // Totals depend on the qty/price fields: recompute on every keystroke.
    final left = [if (_saveError != null) FormBanner(_saveError!), status, customer, lines];
    final right = [delivery, note, crm, history];
    return Form(
      key: _form,
      onChanged: changed,
      child: KeyedSubtree(
        key: ValueKey(_formKeyGen),
        child: wide
            ? SingleChildScrollView(
                padding: AppSpacing.screenPadding,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(flex: 3, child: Column(children: _gap(left))),
                    AppSpacing.hGapLg,
                    Expanded(flex: 2, child: Column(children: _gap(right))),
                  ],
                ),
              )
            : ListView(padding: AppSpacing.screenPadding, children: _gap([...left, ...right])),
      ),
    );
  }

  List<Widget> _gap(List<Widget> xs) => [
    for (final (i, x) in xs.indexed) ...[if (i > 0) AppSpacing.gapLg, x],
  ];
}
