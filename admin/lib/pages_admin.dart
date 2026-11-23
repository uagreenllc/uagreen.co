import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'form.dart';
import 'list.dart';
import 'main.dart' show nav;
import 'pages_content.dart' show editContentBlock;

const siteRoles = {'Admin': 'Адмін', 'Sales': 'Продажі', 'Editor': 'Редактор', 'Hr': 'HR'};

// ── Settings (Editor; the Gospodar card is Admin-only) ──

/// A known setting: key, label, hint, and whether it is an on/off switch
/// (stored as "0" when off — a missing key means on, see the site's useOberig).
typedef _S = ({String key, String label, String? hint, bool toggle, int lines});

_S _s(String key, String label, {String? hint, bool toggle = false, int lines = 1}) =>
    (key: key, label: label, hint: hint, toggle: toggle, lines: lines);

final _groups = <(String, IconData, String?, List<_S>)>[
  ('Контакти', Icons.contact_phone_outlined, 'Шапка, футер, сторінка «Контакти» і розмітка для Google', [
    _s('phones', 'Телефони', hint: 'Через кому, у форматі +380…; перший — у шапці'),
    _s('emails', 'Email-и', hint: 'Через кому; перший — у кнопці «Написати»'),
    _s('address', 'Адреса виробництва (UK)'),
    _s('address_en', 'Адреса виробництва (EN)'),
    _s('address_office', 'Юридична адреса (UK)'),
    _s('address_office_en', 'Юридична адреса (EN)'),
    _s('vacancy_apply_url', 'Посилання «Подати анкету»', hint: 'Google Form для загального відгуку на вакансії'),
  ]),
  ('Соцмережі', Icons.share_outlined, 'Порожнє поле — іконка не показується', [
    _s('socials.instagram', 'Instagram'),
    _s('socials.tiktok', 'TikTok'),
    _s('socials.linkedin', 'LinkedIn'),
    _s('socials.youtube', 'YouTube'),
  ]),
  ('SEO за замовчуванням', Icons.travel_explore, 'Для сторінок без власного SEO', [
    _s('site_title_uk', 'Title сайту (UK)'),
    _s('site_description_uk', 'Description сайту (UK)', lines: 3),
  ]),
  ('Оберіг (портал моніторингу)', Icons.monitor_heart_outlined, 'Блоки й посилання на портал portal.uagreen.co на сайті', [
    _s('oberig.url', 'Адреса порталу', hint: 'Порожньо — https://portal.uagreen.co'),
    _s('oberig.calculator_url', 'Адреса калькулятора генерації', hint: 'Порожньо — <портал>/#/wind-calculator'),
    _s('oberig.home', 'Блок на головній', toggle: true),
    _s('oberig.product', 'Блок на сторінках станцій', toggle: true, hint: 'Лише для товарів з позначкою «Блок Оберіг»'),
    _s('oberig.order', 'Блок на сторінці замовлення', toggle: true),
    _s('oberig.footer', 'Посилання у футері', toggle: true),
  ]),
];

class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});
  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  Map<String, String> _saved = {}, _v = {};
  final _extra = <(TextEditingController, TextEditingController)>[];
  String? _error, _saveError;
  bool _loading = true, _busy = false;
  int _gen = 0;

  static final _known = {for (final g in _groups) ...g.$4.map((s) => s.key)};

  /// Keys other screens own (CRM switches, Доставка, Аналітика): never shown or touched here.
  static bool _elsewhere(String k) =>
      k.startsWith('admin.') || k.startsWith('delivery.') || const {'ga4_id', 'google_ads_id', 'fb_pixel_id', 'stats_disabled'}.contains(k);

  bool get _dirty => jsonEncode(_collect()) != jsonEncode(_saved);

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _show((await api.get('/admin/settings') as J).map((k, v) => MapEntry(k, '$v')));
    } catch (e) {
      _error = '$e';
    }
    if (mounted) setState(() => _loading = false);
  }

  void _show(Map<String, String> m) {
    _saved = m;
    _v = {...m};
    _extra
      ..clear()
      ..addAll([
        for (final e in m.entries)
          if (!_known.contains(e.key) && !_elsewhere(e.key))
            (TextEditingController(text: e.key), TextEditingController(text: e.value)),
      ]);
    _gen++;
  }

  /// What the screen would save: known fields + the free-form rows (+ untouched admin.* keys).
  Map<String, String> _collect() {
    final m = <String, String>{
      for (final e in _v.entries)
        if (_known.contains(e.key) && e.value.trim().isNotEmpty) e.key: e.value.trim(),
      for (final e in _saved.entries)
        if (_elsewhere(e.key)) e.key: e.value,
    };
    for (final (k, v) in _extra) {
      if (k.text.trim().isNotEmpty && v.text.trim().isNotEmpty) m[k.text.trim()] = v.text.trim();
    }
    return Map.fromEntries(m.entries.toList()..sort((a, b) => a.key.compareTo(b.key)));
  }

  Future<void> _save() async {
    if (_busy) return;
    final now = _collect();
    final removed = _saved.keys.where((k) => !now.containsKey(k)).toList();
    if (removed.isNotEmpty &&
        !await showConfirmDialog(context,
            title: 'Видалити ${removed.length} налаштування?',
            message: '${removed.join(', ')}\n\nСайт перестане їх показувати (або візьме значення за замовчуванням).',
            confirmLabel: 'Зберегти і видалити')) {
      return;
    }
    setState(() {
      _busy = true;
      _saveError = null;
    });
    try {
      final res = await api.put('/admin/settings', {...now, for (final k in removed) k: null}) as J;
      if (!mounted) return;
      setState(() => _show(res.map((k, v) => MapEntry(k, '$v'))));
      AppFeedback.showSuccess(context, 'Налаштування збережено — сайт оновиться протягом хвилини');
    } catch (e) {
      if (mounted) setState(() => _saveError = '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  Widget _field(_S s) {
    if (s.toggle) {
      return SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: Text(s.label),
        subtitle: s.hint == null ? null : Text(s.hint!),
        value: _v[s.key] != '0',
        onChanged: (on) => setState(() => _v[s.key] = on ? '1' : '0'),
      );
    }
    return TextFormField(
      initialValue: _v[s.key] ?? '',
      minLines: 1,
      maxLines: s.lines,
      decoration: InputDecoration(labelText: s.label, helperText: s.hint),
      onChanged: (x) => setState(() => _v[s.key] = x),
    );
  }

  @override
  Widget build(BuildContext context) {
    final admin = api.roles.contains('Admin');
    return PopScope(
      canPop: !_dirty,
      child: SaveShortcut(
        onSave: _save,
        child: Focus(
          autofocus: true,
          child: AppAsyncBody(
            isLoading: _loading,
            error: _error,
            onRetry: _load,
            isEmpty: _saved.isEmpty && (_loading || _error != null),
            child: Column(
              children: [
                AppToolbar(
                  actions: [
                    if (_dirty) Text('Є незбережені зміни', style: TextStyle(color: context.semantic.warning)),
                    IconButton(tooltip: 'Перечитати', icon: const Icon(Icons.refresh), onPressed: _load),
                    AppButton(label: 'Зберегти', icon: Icons.save_outlined, isLoading: _busy, onPressed: _dirty ? _save : null),
                  ],
                ),
                Expanded(
                  child: CenteredScrollColumn(
                    maxWidth: AppSizes.dialogContentWidthXl,
                    padding: AppSpacing.screenPadding,
                    builder: (padding) => ListView(
                      key: ValueKey(_gen),
                      padding: padding,
                      children: [
                        if (_saveError != null) ...[FormBanner(_saveError!), AppSpacing.gapLg],
                        if (admin) ...[const _GospodarCard(), AppSpacing.gapLg],
                        for (final (title, icon, hint, fields) in _groups) ...[
                          AppSectionCard(
                            title: title,
                            icon: icon,
                            subtitle: hint,
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                for (final (i, s) in fields.indexed) ...[if (i > 0) AppSpacing.gapMd, _field(s)],
                                if (title.startsWith('Оберіг')) ...[
                                  AppSpacing.gapMd,
                                  Text('Тексти блоків', style: Theme.of(context).textTheme.titleSmall),
                                  AppSpacing.gapSm,
                                  Wrap(
                                    spacing: AppSpacing.sm,
                                    runSpacing: AppSpacing.sm,
                                    children: [
                                      for (final (k, label) in [('oberig.home', 'Головна'), ('oberig.product', 'Товар'), ('oberig.order', 'Замовлення')])
                                        for (final lang in ['uk', 'en'])
                                          OutlinedButton.icon(
                                            icon: const Icon(Icons.edit_outlined, size: AppSizes.iconSm),
                                            label: Text('$label · ${lang.toUpperCase()}'),
                                            onPressed: () => editContentBlock(context, k, lang),
                                          ),
                                    ],
                                  ),
                                  AppSpacing.gapXs,
                                  Text('Без власного тексту сайт показує стандартний.', style: Theme.of(context).textTheme.bodySmall),
                                ],
                              ],
                            ),
                          ),
                          AppSpacing.gapLg,
                        ],
                        AppSectionCard(
                          title: 'Інші ключі',
                          icon: Icons.data_object,
                          subtitle: 'Додаткові налаштування, які читає сайт. Порожнє значення — видалити ключ.',
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              for (final (i, (k, v)) in _extra.indexed)
                                Padding(
                                  key: ObjectKey(k),
                                  padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                                  child: Row(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      SizedBox(
                                        width: 240,
                                        child: TextField(
                                          controller: k,
                                          onChanged: (_) => setState(() {}),
                                          decoration: const InputDecoration(labelText: 'Ключ'),
                                        ),
                                      ),
                                      AppSpacing.hGapSm,
                                      Expanded(
                                        child: TextField(
                                          controller: v,
                                          minLines: 1,
                                          maxLines: 4,
                                          onChanged: (_) => setState(() {}),
                                          decoration: const InputDecoration(labelText: 'Значення'),
                                        ),
                                      ),
                                      IconButton(
                                        tooltip: 'Прибрати',
                                        icon: const Icon(Icons.delete_outline),
                                        onPressed: () => setState(() => _extra.removeAt(i)),
                                      ),
                                    ],
                                  ),
                                ),
                              Align(
                                alignment: Alignment.centerLeft,
                                child: TextButton.icon(
                                  icon: const Icon(Icons.add),
                                  label: const Text('Ключ'),
                                  onPressed: () => setState(() => _extra.add((TextEditingController(), TextEditingController()))),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Gospodar CRM feed: key status, connection test, on/off per source, counters.
class _GospodarCard extends StatefulWidget {
  const _GospodarCard();
  @override
  State<_GospodarCard> createState() => _GospodarCardState();
}

class _GospodarCardState extends State<_GospodarCard> {
  J? _s;
  String? _error, _test;
  bool _testing = false, _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final s = await api.get('/admin/integrations/gospodar') as J;
      if (mounted) setState(() => _s = s);
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    }
  }

  Future<void> _set({bool? orders, bool? contacts}) async {
    final s = _s!;
    final o = orders ?? s['ordersSince'] != null, c = contacts ?? s['contactsOn'] == true;
    if (orders == true &&
        !await showConfirmDialog(context,
            title: 'Передавати замовлення в CRM Господаря?',
            message: 'Нові замовлення автоматично ставатимуть угодами на менеджера з продажу. '
                'Попередні не передаються — їх можна надіслати вручну з картки замовлення.',
            confirmLabel: 'Увімкнути',
            destructive: false)) {
      return;
    }
    if (contacts == false &&
        mounted &&
        !await showConfirmDialog(context,
            title: 'Призупинити передачу запитів у CRM?',
            message: 'Нові запити з контактної форми й консультацій лишатимуться лише в адмінці та пошті, '
                'менеджери в Господарі їх не побачать. Запит, що пролежав понад 3 дні, автоматично вже не піде.',
            confirmLabel: 'Призупинити')) {
      return;
    }
    if (!mounted) return;
    setState(() => _busy = true);
    if (await attempt(context, () => api.put('/admin/integrations/gospodar', {'orders': o, 'contacts': c}), ok: 'Збережено')) {
      await _load();
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final s = _s;
    final theme = Theme.of(context);
    return AppSectionCard(
      title: 'Господар (CRM)',
      icon: Icons.business_center_outlined,
      subtitle: 'Замовлення і запити з сайту — угодами в CRM erp.uagreen.co',
      child: _error != null
          ? FormBanner(_error!)
          : s == null
          ? const LinearProgressIndicator()
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                InfoRow(
                  label: 'Ключ',
                  valueWidget: Align(
                    alignment: Alignment.centerLeft,
                    child: s['configured'] == true
                        ? chip(context, 'Задано на сервері', 'success', icon: Icons.key)
                        : chip(context, 'Не задано', 'error', icon: Icons.key_off),
                  ),
                ),
                InfoRow(label: 'Сервер', value: '${s['baseUrl']}'),
                if (s['configured'] != true) ...[
                  AppSpacing.gapSm,
                  Text(
                    'Ключ інтеграції компанії видає адміністратор Господаря (Компанії → Інтеграція). '
                    'Його вписують у .env сервера як GOSPODAR_INTEGRATION_KEY і перезапускають API — '
                    'в адмінці ключ не зберігається, щоб не потрапити в публічні налаштування.',
                    style: theme.textTheme.bodySmall,
                  ),
                ],
                AppSpacing.gapSm,
                Row(
                  children: [
                    AppButton(
                      label: 'Перевірити з\'єднання',
                      icon: Icons.network_check,
                      variant: AppButtonVariant.secondary,
                      isLoading: _testing,
                      onPressed: () async {
                        setState(() {
                          _testing = true;
                          _test = null;
                        });
                        try {
                          final r = await api.post('/admin/integrations/gospodar/test', null) as J;
                          _test = r['ok'] == true ? 'ok' : '${r['error']}';
                        } catch (e) {
                          _test = '$e';
                        }
                        if (mounted) setState(() => _testing = false);
                      },
                    ),
                    AppSpacing.hGapMd,
                    if (_test == 'ok')
                      chip(context, 'Господар приймає ключ', 'success', icon: Icons.check)
                    else if (_test != null)
                      Expanded(child: Text(_test!, style: TextStyle(color: theme.colorScheme.error))),
                  ],
                ),
                const Divider(height: AppSpacing.xxl),
                for (final (key, label, on, state, stats, section) in [
                  ('orders', 'Замовлення → угоди', s['ordersSince'] != null,
                      s['ordersSince'] == null ? 'Вимкнено' : 'Увімкнено ${fmtDate(s['ordersSince'])}', s['orders'] as J, 'orders'),
                  ('contacts', 'Запити з форм і консультацій → угоди', s['contactsOn'] == true,
                      s['contactsOn'] == true ? 'Автоматично, щойно задано ключ' : 'Призупинено', s['contacts'] as J, 'contacts'),
                ]) ...[
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(label),
                    subtitle: Text('$state · передано ${stats['sent']}'
                        '${(stats['failed'] as num) > 0 ? ' · помилок ${stats['failed']}' : ''}'),
                    value: on,
                    onChanged: _busy || s['configured'] != true
                        ? null
                        : (on) => key == 'orders' ? _set(orders: on) : _set(contacts: on),
                  ),
                  if ((stats['failed'] as num) > 0)
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        icon: const Icon(Icons.error_outline),
                        label: Text('Показати ${stats['failed']} з помилкою'),
                        onPressed: () => nav.go(section, {'crm': 'failed'}),
                      ),
                    ),
                ],
              ],
            ),
    );
  }
}

// ── Users (Admin) ──

class UsersPage extends StatefulWidget {
  const UsersPage({super.key});
  @override
  State<UsersPage> createState() => _UsersPageState();
}

class _UsersPageState extends State<UsersPage> {
  final _list = GlobalKey<PagedListState>();
  List<J>? _all;
  bool _gospodarDown = false, _withRoles = false;

  /// Gospodar returns every user in one call: page and search them here.
  Future<(List<J>, int)> _load(int page, int size, String q) async {
    if (page == 1 || _all == null) {
      final res = await api.get('/admin/users') as J;
      _all = (res['users'] as List).cast<J>();
      final down = res['gospodarAvailable'] != true;
      if (down != _gospodarDown && mounted) setState(() => _gospodarDown = down);
    }
    final ql = q.toLowerCase();
    final rows = _all!
        .where((u) => !_withRoles || u['gospodarAdmin'] == true || (u['roles'] as List).isNotEmpty)
        .where((u) => ql.isEmpty || '${u['fullName']} ${u['email']}'.toLowerCase().contains(ql))
        .toList();
    return (rows.skip((page - 1) * size).take(size).toList(), rows.length);
  }

  Future<void> _toggle(BuildContext context, J u, String role, bool on) async {
    final roles = [...u['roles'] as List]..remove(role);
    if (on) roles.add(role);
    if (!on &&
        !await showConfirmDialog(context,
            title: 'Зняти роль «${siteRoles[role]}»?',
            message: '${u['fullName']} втратить доступ до відповідних розділів адмінки одразу після оновлення сторінки.',
            confirmLabel: 'Зняти роль')) {
      return;
    }
    try {
      final r = await api.put('/admin/users/${u['id']}/roles', {'roles': roles, 'email': u['email'], 'fullName': u['fullName']}) as J;
      _all = [for (final x in _all!) x['id'] == u['id'] ? {...x, 'roles': r['roles']} : x];
      _list.currentState?.replace({...u, 'roles': r['roles']});
    } catch (e) {
      if (context.mounted) AppFeedback.showError(context, '$e');
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    children: [
      if (_gospodarDown)
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.md, AppSpacing.lg, 0),
          child: FormBanner('Господар недоступний — показано лише користувачів, яким уже призначено ролі', color: context.semantic.warning),
        ),
      Expanded(
        child: PagedList(
          key: _list,
          load: _load,
          query: {'withRoles': _withRoles},
          searchHint: "Ім'я або email",
          emptyIcon: Icons.person_search_outlined,
          filters: [
            FilterChip(label: const Text('Лише з доступом'), selected: _withRoles, onSelected: (v) => setState(() => _withRoles = v)),
          ],
          item: (context, u, _) => AppListCard(
            leading: AppAvatar(name: '${u['fullName']}'),
            title: Text('${u['fullName']}'),
            subtitle: Text('${u['email']}'),
            trailing: u['gospodarAdmin'] == true
                ? Tooltip(
                    message: 'Адміністратор Господаря має повний доступ автоматично',
                    child: chip(context, 'Адмін ERP — повний доступ', 'info', icon: Icons.verified_user_outlined),
                  )
                : Wrap(
                    spacing: AppSpacing.xs,
                    children: [
                      for (final e in siteRoles.entries)
                        FilterChip(
                          label: Text(e.value),
                          selected: (u['roles'] as List).contains(e.key),
                          onSelected: (on) => _toggle(context, u, e.key, on),
                        ),
                    ],
                  ),
          ),
        ),
      ),
    ],
  );
}
