import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';
import 'list.dart';
import 'main.dart' show nav;

const _contacts = '/admin/contact-requests', _applications = '/admin/applications';

const applicationStatuses = {
  'New': 'Новий',
  'Reviewed': 'Розглянуто',
  'Interview': 'Співбесіда',
  'Rejected': 'Відмова',
  'Hired': 'Найнято',
};
const _appTone = {'New': 'info', 'Reviewed': 'neutral', 'Interview': 'warning', 'Rejected': 'error', 'Hired': 'success'};

Widget _unreadDot(BuildContext context, J r) => r['isRead'] == true
    ? const SizedBox(width: AppSizes.iconXs)
    : Tooltip(
        message: 'Непрочитане',
        child: Icon(Icons.circle, size: AppSizes.iconXs, color: Theme.of(context).colorScheme.primary),
      );

/// Detail sheet shared by both inboxes: the message, contacts with copy, an
/// internal note, extra sections (status, CV, CRM) and the footer actions.
class InboxDialog extends StatefulWidget {
  const InboxDialog({super.key, required this.path, required this.row, required this.title, required this.extra});
  final String path;
  final J row;
  final String title;
  final List<Widget> Function(BuildContext context, J row, void Function(J) update) extra;

  @override
  State<InboxDialog> createState() => _InboxDialogState();
}

class _InboxDialogState extends State<InboxDialog> with UnsavedChangesGuard {
  late J _r = widget.row;
  late final _note = TextEditingController(text: _r['note'] ?? '');
  bool _busy = false;

  @override
  bool get isDirty => _note.text.trim() != (_r['note'] ?? '');

  @override
  void initState() {
    super.initState();
    if (_r['isRead'] != true) _patch({'isRead': true}, quiet: true);
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _patch(J body, {bool quiet = false}) async {
    setState(() => _busy = true);
    try {
      _r = await api.patch('${widget.path}/${_r['id']}', body) as J;
      summary.refresh();
      if (!quiet && mounted) AppFeedback.showSuccess(context, 'Збережено');
    } catch (e) {
      if (mounted) AppFeedback.showError(context, '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final r = _r;
    return guard(
      child: ResponsiveFormDialog(
        title: widget.title,
        content: SizedBox(
          width: AppSizes.dialogContentWidthLg,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                InfoRow(label: 'Дата', value: fmtDate(r['createdAt'])),
                InfoRow(label: "Ім'я", value: '${r['name']}', copyable: true),
                InfoRow(
                  label: 'Телефон',
                  value: '${r['phone']}',
                  copyable: true,
                  trailing: IconButton(
                    tooltip: 'Подзвонити',
                    icon: const Icon(Icons.call_outlined, size: AppSizes.iconSm),
                    onPressed: () => browser.openTab('tel:${r['phone']}'),
                  ),
                ),
                if (r['email'] != null)
                  InfoRow(
                    label: 'Email',
                    value: '${r['email']}',
                    copyable: true,
                    trailing: IconButton(
                      tooltip: 'Написати',
                      icon: const Icon(Icons.mail_outline, size: AppSizes.iconSm),
                      onPressed: () => browser.openTab('mailto:${r['email']}'),
                    ),
                  ),
                if ((r['message'] as String?)?.isNotEmpty == true) ...[
                  AppSpacing.gapMd,
                  AppPanel(title: 'Повідомлення', child: SelectableText('${r['message']}')),
                ],
                ...widget.extra(context, r, (x) => setState(() => _r = x)),
                AppSpacing.gapMd,
                TextField(
                  controller: _note,
                  minLines: 2,
                  maxLines: 6,
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    labelText: 'Нотатка (бачать лише співробітники)',
                    suffixIcon: isDirty
                        ? IconButton(
                            tooltip: 'Зберегти нотатку',
                            icon: const Icon(Icons.check),
                            onPressed: _busy ? null : () => _patch({'note': _note.text}),
                          )
                        : null,
                  ),
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton.icon(
            icon: Icon(Icons.delete_outline, color: Theme.of(context).colorScheme.error),
            label: Text('Видалити', style: TextStyle(color: Theme.of(context).colorScheme.error)),
            onPressed: () async {
              final cv = r['hasCv'] == true ? 'Файл CV теж буде видалено. ' : '';
              if (await confirmDelete(context, '${r['name']}', '${widget.path}/${r['id']}', warning: cv) && context.mounted) {
                Navigator.pop(context, <String, dynamic>{'id': r['id'], 'deleted': true});
              }
            },
          ),
          TextButton(
            onPressed: () => _patch({'isRead': false}).then((_) {
              if (context.mounted) Navigator.pop(context, _r);
            }),
            child: const Text('Позначити непрочитаним'),
          ),
          FilledButton(
            onPressed: () async {
              if (await confirmClose() && context.mounted) Navigator.pop(context, _r);
            },
            child: const Text('Закрити'),
          ),
        ],
      ),
    );
  }
}

Future<void> _openInbox(BuildContext context, PagedListState list, J r, Widget dialog) async {
  final res = await ResponsiveFormDialog.show<J>(context, builder: (_) => dialog);
  if (res == null) {
    list.reload(); // closed by the barrier: the read flag may have changed
  } else if (res['deleted'] == true) {
    list.removeWhere((x) => x['id'] == res['id']);
  } else {
    list.replace(res);
  }
  summary.refresh();
}

class ContactsPage extends StatefulWidget {
  const ContactsPage({super.key});
  @override
  State<ContactsPage> createState() => _ContactsPageState();
}

class _ContactsPageState extends State<ContactsPage> {
  bool _unread = nav.take('unread') == '1';
  String? _crm = nav.take('crm');

  @override
  Widget build(BuildContext context) => PagedList(
    load: pagedGet(_contacts, {'unread': _unread ? true : null, 'crm': _crm}),
    query: {'unread': _unread, 'crm': _crm},
    searchHint: "Ім'я, телефон, email або текст",
    emptyIcon: Icons.mark_email_read_outlined,
    emptyText: 'Запитів немає',
    filters: [
      FilterChip(label: const Text('Непрочитані'), selected: _unread, onSelected: (v) => setState(() => _unread = v)),
      FilterChip(
        label: const Text('Помилка CRM'),
        selected: _crm == 'failed',
        onSelected: (v) => setState(() => _crm = v ? 'failed' : null),
      ),
    ],
    item: (context, r, list) => AppListCard(
      leading: AppAvatar(name: '${r['name']}'),
      title: Row(
        children: [
          _unreadDot(context, r),
          AppSpacing.hGapSm,
          Expanded(
            child: Text('${r['name']} · ${r['phone']}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: r['isRead'] == true ? null : const TextStyle(fontWeight: AppFontWeights.semibold)),
          ),
        ],
      ),
      subtitle: Text('${r['message']}', maxLines: 2, overflow: TextOverflow.ellipsis),
      isThreeLine: true,
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [Text(fmtDate(r['createdAt'])), AppSpacing.gapXs, crmChip(context, r['crm'])],
      ),
      onTap: () => _openInbox(
        context,
        list,
        r,
        InboxDialog(
          path: _contacts,
          row: r,
          title: 'Запит з сайту',
          extra: (context, r, update) => [
            AppSpacing.gapMd,
            AppPanel(
              title: 'Господар (CRM)',
              child: CrmPanel(
                crm: (r['crm'] as J?) ?? {},
                onPush: () async {
                  try {
                    final x = await api.post('$_contacts/${r['id']}/crm', null) as J;
                    update(x);
                    final err = x['crm']?['error'];
                    if (context.mounted) {
                      err == null ? AppFeedback.showSuccess(context, 'Угоду створено') : AppFeedback.showError(context, '$err');
                    }
                  } catch (e) {
                    if (context.mounted) AppFeedback.showError(context, '$e');
                  }
                },
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

class ApplicationsPage extends StatefulWidget {
  const ApplicationsPage({super.key});
  @override
  State<ApplicationsPage> createState() => _ApplicationsPageState();
}

class _ApplicationsPageState extends State<ApplicationsPage> {
  bool _unread = nav.take('unread') == '1';
  String? _status = nav.take('status'), _vacancy = nav.take('vacancyId');
  Map<String, String> _vacancies = {};

  @override
  void initState() {
    super.initState();
    api.get('/admin/vacancies', {'pageSize': 200}).then((r) {
      if (mounted) {
        setState(() => _vacancies = {for (final v in (r as J)['items'] as List) v['id'] as String: v['titleUk'] as String});
      }
    }).ignore();
  }

  Future<void> _downloadCv(BuildContext context, J r) => attempt(
    context,
    () async => browser.saveBytes(await api.bytes('$_applications/${r['id']}/cv'), r['cvFileName'] ?? 'cv'),
  );

  @override
  Widget build(BuildContext context) => PagedList(
    load: pagedGet(_applications, {'unread': _unread ? true : null, 'status': _status, 'vacancyId': _vacancy}),
    query: {'unread': _unread, 'status': _status, 'vacancyId': _vacancy},
    searchHint: "Ім'я, телефон або email",
    emptyIcon: Icons.badge_outlined,
    emptyText: 'Відгуків немає',
    filters: [
      FilterChip(label: const Text('Непрочитані'), selected: _unread, onSelected: (v) => setState(() => _unread = v)),
      filterChip(
        icon: Icons.flag_outlined,
        tooltip: 'Статус',
        allLabel: 'Усі статуси',
        options: applicationStatuses,
        value: _status,
        onSelected: (v) => setState(() => _status = v),
      ),
      if (_vacancies.isNotEmpty)
        filterChip(
          icon: Icons.work_outline,
          tooltip: 'Вакансія',
          allLabel: 'Усі вакансії',
          options: _vacancies,
          value: _vacancy,
          onSelected: (v) => setState(() => _vacancy = v),
        ),
    ],
    item: (context, r, list) => AppListCard(
      leading: AppAvatar(name: '${r['name']}'),
      title: Row(
        children: [
          _unreadDot(context, r),
          AppSpacing.hGapSm,
          Expanded(
            child: Text('${r['name']} · ${r['phone']}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: r['isRead'] == true ? null : const TextStyle(fontWeight: AppFontWeights.semibold)),
          ),
        ],
      ),
      subtitle: Wrap(
        spacing: AppSpacing.sm,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text(r['vacancyTitle'] ?? 'Без вакансії (загальний)'),
          chip(context, applicationStatuses[r['status']] ?? '${r['status']}', _appTone[r['status']] ?? 'neutral'),
          if (r['note'] != null) const Tooltip(message: 'Є нотатка', child: Icon(Icons.sticky_note_2_outlined, size: AppSizes.iconSm)),
        ],
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(fmtDate(r['createdAt'])),
          if (r['hasCv'] == true)
            IconButton(tooltip: 'Завантажити CV', icon: const Icon(Icons.download_outlined), onPressed: () => _downloadCv(context, r)),
        ],
      ),
      onTap: () => _openInbox(
        context,
        list,
        r,
        InboxDialog(
          path: _applications,
          row: r,
          title: 'Відгук: ${r['vacancyTitle'] ?? 'без вакансії'}',
          extra: (context, r, update) => [
            AppSpacing.gapMd,
            DropdownButtonFormField<String>(
              initialValue: r['status'],
              decoration: const InputDecoration(labelText: 'Статус кандидата'),
              items: [for (final s in applicationStatuses.entries) DropdownMenuItem(value: s.key, child: Text(s.value))],
              onChanged: (v) async {
                try {
                  update(await api.patch('$_applications/${r['id']}', {'status': v}) as J);
                } catch (e) {
                  if (context.mounted) AppFeedback.showError(context, '$e');
                }
              },
            ),
            if (r['hasCv'] == true) ...[
              AppSpacing.gapMd,
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  icon: const Icon(Icons.download_outlined),
                  label: Text('CV: ${r['cvFileName'] ?? 'файл'}'),
                  onPressed: () => _downloadCv(context, r),
                ),
              ),
            ],
          ],
        ),
      ),
    ),
  );
}
