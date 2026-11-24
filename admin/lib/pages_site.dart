import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'form.dart';
import 'list.dart';
import 'pages_orders.dart' show deliveries;

/// A page of string settings: load → edit → save only what changed (an empty value
/// deletes the key). Kit chrome, unsaved-changes prompt when leaving, Ctrl+S.
class KeyValuePage extends StatefulWidget {
  const KeyValuePage({
    super.key,
    required this.load,
    required this.save,
    required this.build,
    this.intro,
    this.saved = 'Збережено',
    this.scrolls = true,
  });

  final Future<J> Function() load;

  /// Gets the changed keys (null = delete), returns the fresh values.
  final Future<J> Function(J changes) save;
  final List<Widget> Function(BuildContext context, KeyValueState page) build;
  final String? intro, saved;

  /// False for a body that fills the height itself (tabs with editors).
  final bool scrolls;

  @override
  State<KeyValuePage> createState() => KeyValueState();
}

class KeyValueState extends State<KeyValuePage> {
  final _form = GlobalKey<FormState>();
  J _saved = {}, values = {};
  String? _error, _saveError;
  bool _loading = true, _busy = false;
  int _gen = 0;

  bool get dirty => jsonEncode(_norm(values)) != jsonEncode(_norm(_saved));

  static J _norm(J m) => {
    for (final e in (m.entries.toList()..sort((a, b) => a.key.compareTo(b.key))))
      if ('${e.value ?? ''}'.trim().isNotEmpty) e.key: '${e.value}'.trim(),
  };

  /// Field key for a fresh build after a save (initial values re-read).
  Key fieldKey(String k) => ValueKey('$k#$_gen');

  void set(String key, String? v) => setState(() => values[key] = v);
  String? get(String key) => values[key] as String?;

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
      _show(await widget.load());
    } catch (e) {
      _error = '$e';
    }
    if (mounted) setState(() => _loading = false);
  }

  void _show(J m) {
    _saved = {for (final e in m.entries) e.key: e.value?.toString()};
    values = {..._saved};
    _gen++;
  }

  Future<void> save() async {
    if (_busy || !dirty) return;
    if (!_form.currentState!.validate()) return AppFeedback.showError(context, 'Перевірте позначені поля');
    final now = _norm(values), was = _norm(_saved);
    final changes = <String, dynamic>{
      for (final k in {...now.keys, ...was.keys})
        if (now[k] != was[k]) k: now[k],
    };
    setState(() {
      _busy = true;
      _saveError = null;
    });
    try {
      final fresh = await widget.save(changes);
      if (!mounted) return;
      setState(() => _show(fresh));
      AppFeedback.showSuccess(context, widget.saved!);
    } on SaveCancelled {
      // the user backed out of the confirmation
    } catch (e) {
      if (mounted) setState(() => _saveError = '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final children = [
      if (widget.intro != null) ...[
        Text(widget.intro!, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: context.palette.textSecondary)),
        AppSpacing.gapLg,
      ],
      if (_saveError != null) ...[FormBanner(_saveError!), AppSpacing.gapLg],
      if (!_loading && _error == null) ...widget.build(context, this),
    ];
    return PopScope(
      canPop: !dirty,
      child: SaveShortcut(
        onSave: save,
        child: AppAsyncBody(
          isLoading: _loading,
          error: _error,
          onRetry: _load,
          isEmpty: _loading || _error != null,
          child: Form(
            key: _form,
            child: Column(
              children: [
                AppToolbar(
                  actions: [
                    if (dirty) Text('Є незбережені зміни', style: TextStyle(color: context.semantic.warning)),
                    IconButton(tooltip: 'Перечитати', icon: const Icon(Icons.refresh), onPressed: _load),
                    AppButton(label: 'Зберегти', icon: Icons.save_outlined, isLoading: _busy, onPressed: dirty ? save : null),
                  ],
                ),
                Expanded(
                  child: widget.scrolls
                      ? CenteredScrollColumn(
                          maxWidth: AppSizes.dialogContentWidthXl,
                          padding: AppSpacing.screenPadding,
                          builder: (padding) => ListView(padding: padding, children: children),
                        )
                      : Padding(
                          padding: AppSpacing.screenPadding,
                          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: children),
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

/// TextFormField bound to one key of a [KeyValueState].
Widget kvField(
  KeyValueState p,
  String key,
  String label, {
  String? hint,
  String? helper,
  int lines = 1,
  bool enabled = true,
  String? Function(String v)? validate,
}) => TextFormField(
  key: p.fieldKey(key),
  initialValue: p.get(key) ?? '',
  enabled: enabled,
  minLines: 1,
  maxLines: lines,
  decoration: InputDecoration(labelText: label, hintText: hint, helperText: helper),
  validator: validate == null ? null : (v) => v!.trim().isEmpty ? null : validate(v.trim()),
  onChanged: (v) => p.set(key, v),
);

// ── Платіжні системи (Admin): checkout methods + requisites for the IBAN transfer ──

class PaymentsPage extends StatelessWidget {
  const PaymentsPage({super.key});

  static const _methods = [
    ('Card', Icons.credit_card, 'Онлайн карткою — monobank',
        'Visa / Mastercard, Apple Pay, Google Pay через інтернет-еквайринг monobank. '
            'Оплата підтверджується автоматично; токен задається на сервері.'),
    ('Invoice', Icons.account_balance, 'Вручну — переказ на IBAN',
        'Після замовлення клієнт бачить реквізити компанії й суму та платить переказом з будь-якого банку. '
            'Надходження перевіряє менеджер і ставить статус «Оплачено».'),
    ('Credit', Icons.savings_outlined, 'Кредит 0 % (державна програма)',
        "Менеджер зв'язується з клієнтом і оформлює кредит у банку-партнері."),
    ('MonoParts', Icons.event_repeat, 'Покупка частинами monobank',
        'Клієнт підтверджує заявку в застосунку monobank. Коли ставите «Відправлено», сайт підтверджує видачу '
            'в банку — після цього банк перераховує гроші.'),
  ];
  // A copy of Gospodar's Компанії → реквізити, pulled by the API (on open and every 15 min); read-only here.
  static const _requisites = [
    ('pay_recipient', 'Отримувач'),
    ('pay_edrpou', 'ЄДРПОУ (або РНОКПП для ФОП)'),
    ('pay_iban', 'IBAN'),
    ('pay_bank', 'Банк'),
  ];

  /// Adds `_cfg_<method>` = '0' for methods without bank keys on the server. Display only: the values never
  /// change, so KeyValuePage never sends them back.
  static Future<J> _withConfigured(J values) async {
    final cfg = await api.get('/admin/payments/configured') as J;
    return {...values, for (final e in cfg.entries) '_cfg_${e.key}': e.value == true ? '1' : '0'};
  }

  static Widget _notConfigured(ThemeData theme) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Text('Не налаштовано на сервері — сайт цей спосіб не показує.',
        style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error)),
  );

  @override
  Widget build(BuildContext context) => KeyValuePage(
    intro: 'Способи оплати на сторінці оформлення замовлення. Вимкнений спосіб клієнт не бачить, і сайт його не прийме.',
    load: () async => _withConfigured(await api.get('/admin/payments') as J),
    save: (changes) async {
      final turningOff = changes.entries.where((e) => e.key.startsWith('pay_') && e.value == 'off');
      if (turningOff.isNotEmpty &&
          !await showConfirmDialog(context,
              title: 'Вимкнути спосіб оплати?',
              message: 'Клієнти більше не зможуть обрати ${turningOff.map((e) => '«${e.key.substring(4)}»').join(', ')} '
                  'на сайті. Уже оформлені замовлення не зміняться.',
              confirmLabel: 'Вимкнути',
              destructive: false)) {
        throw const SaveCancelled();
      }
      return _withConfigured(await api.put('/admin/payments', changes) as J);
    },
    build: (context, p) {
      final theme = Theme.of(context);
      return [
        for (final (key, icon, title, desc) in _methods) ...[
          AppSectionCard(
            title: title,
            icon: icon,
            subtitle: desc,
            action: Switch(
              value: p.get('pay_$key') != 'off',
              onChanged: (on) => p.set('pay_$key', on ? null : 'off'),
            ),
            child: key == 'MonoParts'
                ? Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (p.get('_cfg_MonoParts') == '0') _notConfigured(theme),
                      kvField(p, 'pay_parts_max', 'Максимум платежів', helper: 'від 3 до 25; порожньо — 12',
                          enabled: p.get('pay_MonoParts') != 'off',
                          validate: (v) => (int.tryParse(v) ?? 0) >= 3 && (int.tryParse(v) ?? 0) <= 25 ? null : 'Від 3 до 25'),
                    ],
                  )
                : key == 'Card'
                ? (p.get('_cfg_Card') == '0' ? _notConfigured(theme) : const SizedBox.shrink())
                : key != 'Invoice'
                ? const SizedBox.shrink()
                : Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text('Реквізити компанії', style: theme.textTheme.titleSmall),
                      Text(
                        p.get('pay_iban') == null
                            ? 'Беруться з Господаря (Компанії → реквізити), але IBAN там не заповнено — '
                                'клієнт не побачить реквізитів для оплати.'
                            : 'Беруться з Господаря (Компанії → реквізити) й оновлюються самі. Змінюйте їх там.',
                        style: theme.textTheme.bodySmall?.copyWith(
                            color: p.get('pay_iban') == null ? context.semantic.warning : context.palette.textSecondary),
                      ),
                      for (final (k, label) in _requisites) ...[
                        AppSpacing.gapMd,
                        TextFormField(
                          key: p.fieldKey(k),
                          initialValue: k == 'pay_iban' ? p.get(k)?.replaceAllMapped(RegExp(r'.{4}(?=.)'), (m) => '${m[0]} ') : p.get(k),
                          readOnly: true,
                          decoration: InputDecoration(labelText: label, hintText: '—'),
                        ),
                      ],
                      AppSpacing.gapMd,
                      kvField(p, 'pay_purpose', 'Призначення платежу',
                          helper: '{n} — номер замовлення; порожньо: «Оплата за замовлення №{n}»',
                          enabled: p.get('pay_Invoice') != 'off'),
                    ],
                  ),
          ),
          AppSpacing.gapLg,
        ],
      ];
    },
  );
}

// ── Доставка: methods on/off + a note under each at checkout (public settings) ──

class DeliveryPage extends StatelessWidget {
  const DeliveryPage({super.key});
  static const _langs = ['uk', 'en', 'cs'];

  @override
  Widget build(BuildContext context) => KeyValuePage(
    intro: 'Способи доставки в оформленні замовлення. Примітка — дрібним текстом під назвою (термін, адреса).',
    load: () async {
      final all = await api.get('/admin/settings') as J;
      return {for (final e in all.entries) if (e.key.startsWith('delivery.')) e.key: e.value};
    },
    save: (changes) async {
      final all = await api.put('/admin/settings', changes) as J;
      return {for (final e in all.entries) if (e.key.startsWith('delivery.')) e.key: e.value};
    },
    build: (context, p) => [
      for (final d in deliveries.entries) ...[
        AppSectionCard(
          title: d.value,
          icon: Icons.local_shipping_outlined,
          subtitle: p.get('delivery.${d.key}.off') == null ? 'Увімкнено' : 'Вимкнено — клієнт не побачить цей спосіб',
          action: Switch(
            value: p.get('delivery.${d.key}.off') == null,
            onChanged: (on) => p.set('delivery.${d.key}.off', on ? null : '1'),
          ),
          child: Wrap(
            spacing: AppSpacing.md,
            runSpacing: AppSpacing.md,
            children: [
              for (final l in _langs)
                SizedBox(
                  width: 280,
                  child: kvField(p, 'delivery.${d.key}.note.$l', 'Примітка (${l.toUpperCase()})',
                      enabled: p.get('delivery.${d.key}.off') == null),
                ),
            ],
          ),
        ),
        AppSpacing.gapLg,
      ],
    ],
  );
}

// ── Аналітика: counter ids the site adds to every page ──

/// (settings key, title, field label, example, format) — the keys frontend/composables/useAnalytics.ts reads.
final _analytics = [
  ('ga4_id', 'Google Аналітика', 'ID потоку даних GA4', 'G-XXXXXXXXXX', RegExp(r'^G-[A-Z0-9]+$')),
  ('google_ads_id', 'Google Ads', 'Тег Google Ads (конверсії, ремаркетинг)', 'AW-1234567890', RegExp(r'^AW-\d+$')),
  ('fb_pixel_id', 'Піксель Facebook', 'ID пікселя Meta', '1234567890123456', RegExp(r'^\d+$')),
];

class AnalyticsPage extends StatelessWidget {
  const AnalyticsPage({super.key});

  @override
  Widget build(BuildContext context) => KeyValuePage(
    intro: 'Сайт додає ці лічильники на всі сторінки. Порожнє поле — сервіс відключено.',
    load: () async {
      final all = await api.get('/admin/settings') as J;
      return {for (final a in _analytics) a.$1: all[a.$1]};
    },
    save: (changes) async {
      final all = await api.put('/admin/settings', changes) as J;
      return {for (final a in _analytics) a.$1: all[a.$1]};
    },
    build: (context, p) => [
      for (final (key, title, label, example, format) in _analytics) ...[
        AppSectionCard(
          title: title,
          icon: Icons.insights_outlined,
          action: p.get(key)?.isNotEmpty == true
              ? chip(context, 'Підключено', 'success', icon: Icons.check)
              : chip(context, 'Не підключено', 'neutral'),
          child: kvField(p, key, label, hint: example, validate: (v) => format.hasMatch(v) ? null : 'Формат: $example'),
        ),
        AppSpacing.gapLg,
      ],
    ],
  );
}

// ── Сторонній код (Admin): raw HTML/JS/CSS pasted into every page of the site ──

class CodePage extends StatelessWidget {
  const CodePage({super.key});
  static const _tabs = {'code_head': '<HEAD>', 'code_body_start': 'Після <BODY>', 'code_body_end': 'Перед </BODY>', 'code_css': 'CSS'};

  @override
  Widget build(BuildContext context) => KeyValuePage(
    scrolls: false,
    saved: 'Збережено — діє з наступного завантаження сторінки сайту',
    intro: 'Код вставляється як є на кожну сторінку сайту (лічильники, віджети, пікселі). CSS — без тегу <style>.',
    load: () async => await api.get('/admin/code') as J,
    save: (changes) async {
      if (!await showConfirmDialog(context,
          title: 'Опублікувати код на сайті?',
          message: 'Код з\'явиться на всіх сторінках одразу. Помилка в скрипті може зламати сайт для всіх відвідувачів.',
          confirmLabel: 'Опублікувати',
          destructive: false)) {
        throw const SaveCancelled();
      }
      return await api.put('/admin/code', changes) as J;
    },
    build: (context, p) => [
      Expanded(
        child: DefaultTabController(
          length: _tabs.length,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppBodyTabBar(tabs: [for (final e in _tabs.entries) Tab(text: '${e.value}${(p.get(e.key) ?? '').isEmpty ? '' : ' •'}')]),
              AppSpacing.gapMd,
              Expanded(
                child: TabBarView(
                  children: [
                    for (final k in _tabs.keys)
                      TextFormField(
                        key: p.fieldKey(k),
                        initialValue: p.get(k) ?? '',
                        expands: true,
                        maxLines: null,
                        textAlignVertical: TextAlignVertical.top,
                        style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
                        decoration: const InputDecoration(alignLabelWithHint: true),
                        onChanged: (v) => p.set(k, v),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ],
  );
}

// ── Канали збуту: feed links each marketplace pulls on a schedule ──

class FeedsPage extends StatefulWidget {
  const FeedsPage({super.key});
  @override
  State<FeedsPage> createState() => _FeedsPageState();
}

class _FeedsPageState extends State<FeedsPage> {
  static const _channels = [
    ('Prom.ua', 'prom', Icons.storefront_outlined, 'Prom.ua → Імпорт товарів → за посиланням, з автооновленням.'),
    ('Google Merchant Center', 'google', Icons.shopping_bag_outlined,
        'Merchant Center → Джерела товарів → Додати → файл за посиланням.'),
    ('Facebook & Instagram Commerce Manager', 'google', Icons.camera_alt_outlined,
        'Commerce Manager → Каталог → Джерела даних → Запланований фід (той самий фід, що й для Google).'),
  ];
  late Future<Object?> _urls = api.get('/admin/feeds');

  @override
  Widget build(BuildContext context) => FutureBuilder(
    future: _urls,
    builder: (context, s) => AppAsyncBody(
      isLoading: s.connectionState != ConnectionState.done,
      error: s.hasError ? '${s.error}' : null,
      onRetry: () => setState(() => _urls = api.get('/admin/feeds')),
      isEmpty: !s.hasData,
      child: !s.hasData
          ? const SizedBox.shrink()
          : CenteredScrollColumn(
              maxWidth: AppSizes.dialogContentWidthXl,
              padding: AppSpacing.screenPadding,
              builder: (padding) => ListView(
                padding: padding,
                children: [
                  Text('Опубліковані товари, ціни в UAH, українською. Канал сам оновлює фід за розкладом.',
                      style: TextStyle(color: context.palette.textSecondary)),
                  AppSpacing.gapLg,
                  for (final (name, key, icon, how) in _channels) ...[
                    AppSectionCard(
                      title: name,
                      icon: icon,
                      subtitle: how,
                      child: InfoRow(label: 'Посилання', value: '${(s.data as J)[key]}', copyable: true),
                    ),
                    AppSpacing.gapLg,
                  ],
                ],
              ),
            ),
    ),
  );
}

// ── Відгуки клієнтів (Editor): testimonials on the home page ──

const _reviewFields = [
  F('authorUk', "Ім'я, місто (UK)", required: true),
  F('authorEn', "Ім'я, місто (EN)"),
  F('authorCs', "Ім'я, місто (CS)"),
  F('textUk', 'Текст відгуку (UK)', t: T.multiline, required: true),
  F('textEn', 'Текст відгуку (EN)', t: T.multiline),
  F('textCs', 'Текст відгуку (CS)', t: T.multiline),
  F('photoKey', 'Фото', t: T.image, folder: 'content'),
  F('published', 'Показувати на сайті', t: T.bool),
  F('sort', 'Порядок', t: T.int, hint: 'Менше число — раніше на головній'),
];

class ReviewsPage extends StatefulWidget {
  const ReviewsPage({super.key});
  @override
  State<ReviewsPage> createState() => _ReviewsPageState();
}

class _ReviewsPageState extends State<ReviewsPage> {
  final _list = GlobalKey<PagedListState>();

  Future<void> _edit(J? row) async {
    if (await editResource(
          context,
          path: '/admin/reviews',
          noun: 'відгук',
          fields: _reviewFields,
          row: row,
          blank: () => {'published': true, 'sort': 0},
          title: (d) => d['authorUk'] ?? 'Відгук',
        ) ==
        true) {
      _list.currentState?.reload();
    }
  }

  @override
  Widget build(BuildContext context) => PagedList(
    key: _list,
    load: pagedGet('/admin/reviews'),
    searchHint: 'Автор або текст',
    emptyIcon: Icons.reviews_outlined,
    emptyText: 'Відгуків ще немає',
    actions: [FilledButton.icon(onPressed: () => _edit(null), icon: const Icon(Icons.add), label: const Text('Відгук'))],
    item: (context, r, list) => AppListCard(
      leading: r['photoKey'] == null ? AppAvatar(name: '${r['authorUk']}') : thumb(r['photoKey']),
      title: Row(
        children: [
          Flexible(child: Text('${r['authorUk']}', overflow: TextOverflow.ellipsis)),
          if (r['published'] != true) ...[AppSpacing.hGapSm, chip(context, 'Прихований', 'neutral')],
        ],
      ),
      subtitle: Text('${r['textUk']}', maxLines: 2, overflow: TextOverflow.ellipsis),
      isThreeLine: true,
      trailing: IconButton(
        tooltip: 'Видалити',
        icon: const Icon(Icons.delete_outline),
        onPressed: () async {
          if (await confirmDelete(context, '${r['authorUk']}', '/admin/reviews/${r['id']}', warning: 'Відгук зникне з головної. ')) {
            list.removeWhere((x) => x['id'] == r['id']);
          }
        },
      ),
      onTap: () => _edit(r),
    ),
  );
}
