import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';
import 'main.dart' show nav;
import 'pages_orders.dart' show OrderPage, orderStatusChip;

/// "Огляд": what needs attention now, per role — each tile opens the filtered list.
class DashboardPage extends StatefulWidget {
  const DashboardPage({super.key});
  @override
  State<DashboardPage> createState() => _DashboardPageState();
}

class _DashboardPageState extends State<DashboardPage> {
  @override
  void initState() {
    super.initState();
    summary.refresh();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: summary,
    builder: (context, _) {
      final s = summary.data;
      final theme = Theme.of(context);
      final sem = context.semantic;
      final orders = s['orders'] as J?, products = s['products'] as J?;
      final contacts = s['contacts'] as J?, apps = s['applications'] as J?;
      int n(Object? v) => (v as num?)?.toInt() ?? 0;
      final crmFailed = n(orders?['crmFailed']) + n(contacts?['crmFailed']);

      final tiles = <Widget>[
        if (orders != null) ...[
          KpiTile(
            icon: Icons.fiber_new_outlined,
            accent: sem.info,
            value: '${n(orders['new'])}',
            label: 'Нові замовлення',
            tint: n(orders['new']) > 0 ? (color: sem.info, strength: 0.5) : null,
            onTap: () => nav.go('orders', {'status': 'New'}),
          ),
          KpiTile(
            icon: Icons.hourglass_empty,
            accent: sem.warning,
            value: '${n(orders['awaitingPayment'])}',
            label: 'Очікують оплату',
            onTap: () => nav.go('orders', {'status': 'AwaitingPayment'}),
          ),
          KpiTile(
            icon: Icons.payments_outlined,
            accent: sem.success,
            value: money(orders['revenue30d']),
            label: 'Оплачено за 30 днів',
            footer: Text('${n(orders['count30d'])} замовлень за 30 днів', style: theme.textTheme.bodySmall),
          ),
        ],
        if (contacts != null)
          KpiTile(
            icon: Icons.mark_email_unread_outlined,
            accent: theme.colorScheme.primary,
            value: '${n(contacts['unread'])}',
            label: 'Непрочитані запити',
            tint: n(contacts['unread']) > 0 ? (color: theme.colorScheme.primary, strength: 0.5) : null,
            onTap: () => nav.go('contacts', {'unread': '1'}),
          ),
        if (apps != null)
          KpiTile(
            icon: Icons.badge_outlined,
            accent: theme.colorScheme.primary,
            value: '${n(apps['unread'])}',
            label: 'Нові відгуки на вакансії',
            onTap: () => nav.go('applications', {'unread': '1'}),
          ),
        if (crmFailed > 0)
          KpiTile(
            icon: Icons.link_off,
            accent: theme.colorScheme.error,
            value: '$crmFailed',
            label: 'Не дійшли в CRM',
            tint: (color: theme.colorScheme.error, strength: 0.6),
            onTap: () => nav.go(n(orders?['crmFailed']) > 0 ? 'orders' : 'contacts', {'crm': 'failed'}),
          ),
      ];

      final recent = (orders?['recent'] as List? ?? const []).cast<J>();
      return CenteredScrollColumn(
        maxWidth: 1200,
        padding: AppSpacing.screenPadding,
        builder: (padding) => RefreshIndicator(
          onRefresh: summary.refresh,
          child: ListView(
            padding: padding,
            physics: const AlwaysScrollableScrollPhysics(),
            children: [
              Text('Вітаємо, ${'${api.user?['fullName'] ?? ''}'.split(' ').first}!', style: theme.textTheme.headlineSmall),
              AppSpacing.gapXs,
              Text('Що потребує уваги на сайті зараз', style: theme.textTheme.bodyMedium?.copyWith(color: context.palette.textTertiary)),
              AppSpacing.gapXl,
              if (s.isEmpty)
                const Padding(padding: AppSpacing.paddingXl, child: AppLoadingView())
              else ...[
                if (tiles.isNotEmpty)
                  Wrap(
                    spacing: AppSpacing.md,
                    runSpacing: AppSpacing.md,
                    children: [for (final t in tiles) SizedBox(width: 270, child: t)],
                  ),
                if (products != null) ...[
                  AppSpacing.gapXl,
                  AppSectionCard(
                    title: 'Каталог',
                    icon: Icons.inventory_2_outlined,
                    subtitle: '${n(products['total'])} товарів',
                    child: Wrap(
                      spacing: AppSpacing.sm,
                      runSpacing: AppSpacing.sm,
                      children: [
                        MetaChip(label: 'Чернеток: ${n(products['drafts'])}', icon: Icons.edit_note, onTap: () => nav.go('products')),
                        MetaChip(
                          label: 'Без фото: ${n(products['noPhoto'])}',
                          icon: Icons.image_not_supported_outlined,
                          emphasized: n(products['noPhoto']) > 0,
                          onTap: () => nav.go('products'),
                        ),
                        MetaChip(label: 'Немає в наявності: ${n(products['outOfStock'])}', icon: Icons.remove_shopping_cart_outlined,
                            onTap: () => nav.go('products')),
                      ],
                    ),
                  ),
                ],
                if (recent.isNotEmpty) ...[
                  AppSpacing.gapXl,
                  AppSectionCard(
                    title: 'Останні замовлення',
                    icon: Icons.receipt_long_outlined,
                    action: TextButton(onPressed: () => nav.go('orders'), child: const Text('Усі')),
                    child: Column(
                      children: [
                        for (final o in recent)
                          AppPanelRow(
                            title: '№${o['number']} · ${o['customerName']}',
                            subtitle: fmtDate(o['createdAt']),
                            trailing: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                orderStatusChip(context, o['status']),
                                AppSpacing.hGapMd,
                                Text(money(o['total']), style: theme.textTheme.titleSmall?.numeric),
                              ],
                            ),
                            onTap: () async {
                              await Navigator.of(context).push(MaterialPageRoute<J>(builder: (_) => OrderPage(id: o['id'])));
                              summary.refresh();
                            },
                          ),
                      ],
                    ),
                  ),
                ],
                AppSpacing.gapXl,
                AppSectionCard(
                  title: 'Пов\'язані системи',
                  icon: Icons.hub_outlined,
                  child: Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: [
                      for (final (k, label, icon) in [
                        ('site', 'Сайт uagreen.co', Icons.public),
                        ('erp', 'Господар — ERP і CRM', Icons.business_center_outlined),
                        ('oberig', 'Оберіг — портал моніторингу', Icons.monitor_heart_outlined),
                      ])
                        if (summary.link(k) case final url?)
                          OutlinedButton.icon(icon: Icon(icon), label: Text(label), onPressed: () => browser.openTab(url)),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
      );
    },
  );
}
