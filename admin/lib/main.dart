import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:uagreen_ui/uagreen_ui.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'dashboard.dart';
import 'pages_admin.dart';
import 'pages_catalog.dart';
import 'pages_content.dart';
import 'pages_inbox.dart';
import 'pages_orders.dart';

void main() {
  nav.key; // read #/section now: Flutter's URL strategy rewrites the address to "/" on start
  runApp(const AdminApp());
  api.boot();
}

class Section {
  const Section(this.key, this.title, this.icon, this.roles, this.build, {this.badge});
  final String key, title;
  final IconData icon;

  /// Any of these (or Admin) — mirrors the backend policies in SiteRoles.cs.
  final List<String> roles;
  final Widget Function() build;

  /// Count shown next to the menu item, from `/admin/summary`.
  final int Function(J summary)? badge;
}

int _n(Object? v) => (v as num?)?.toInt() ?? 0;

final sections = [
  Section('overview', 'Огляд', Icons.space_dashboard_outlined, const [], () => const DashboardPage()),
  Section('orders', 'Замовлення', Icons.receipt_long_outlined, const ['Sales'], () => const OrdersPage(),
      badge: (s) => _n(s['orders']?['new'])),
  Section('products', 'Товари', Icons.inventory_2_outlined, const ['Sales'], () => const ProductsPage()),
  Section('categories', 'Категорії', Icons.category_outlined, const ['Sales'], () => const CategoriesPage()),
  Section('contacts', 'Контактні запити', Icons.mail_outline, const ['Sales', 'Editor'], () => const ContactsPage(),
      badge: (s) => _n(s['contacts']?['unread'])),
  Section('blog', 'Блог', Icons.article_outlined, const ['Editor'], () => const BlogPage()),
  Section('content', 'Контент сторінок', Icons.web_outlined, const ['Editor'], () => const ContentPage()),
  Section('redirects', 'Редиректи', Icons.alt_route, const ['Editor'], () => const RedirectsPage()),
  Section('settings', 'Налаштування', Icons.tune, const ['Editor'], () => const SettingsPage()),
  Section('vacancies', 'Вакансії', Icons.work_outline, const ['Hr'], () => const VacanciesPage()),
  Section('applications', 'Відгуки на вакансії', Icons.badge_outlined, const ['Hr'], () => const ApplicationsPage(),
      badge: (s) => _n(s['applications']?['unread'])),
  Section('users', 'Користувачі', Icons.manage_accounts_outlined, const ['Admin'], () => const UsersPage()),
];

List<Section> sectionsFor(List<String> roles) =>
    sections.where((s) => roles.contains('Admin') || s.roles.isEmpty || s.roles.any(roles.contains)).toList();

/// Light/dark from the kit; the choice is remembered in localStorage.
final themeMode = ValueNotifier(switch (browser.storageGet('uag.theme')) {
  'dark' => ThemeMode.dark,
  'light' => ThemeMode.light,
  _ => ThemeMode.system,
});

class AdminApp extends StatelessWidget {
  const AdminApp({super.key});

  @override
  Widget build(BuildContext context) => ValueListenableBuilder(
    valueListenable: themeMode,
    builder: (_, mode, _) => MaterialApp(
      title: 'UA GREEN — адмінка',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: mode,
      scrollBehavior: AppScrollBehavior(),
      locale: const Locale('uk'),
      supportedLocales: const [Locale('uk')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      home: ListenableBuilder(
        listenable: api,
        builder: (_, _) => api.booting
            ? const Scaffold(body: AppLoadingView())
            : api.user == null
            ? const LoginPage()
            : Shell(key: ValueKey(api.user!['id'])),
      ),
    ),
  );
}

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});
  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController(), _password = TextEditingController();
  String? _error;
  bool _busy = false, _hidden = true;

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await api.login(_email.text.trim(), _password.text);
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: AppSpacing.screenPadding,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: AppSizes.dialogContentWidthMd),
            child: Card(
              child: Padding(
                padding: AppSpacing.dialogPadding,
                child: Form(
                  key: _form,
                  child: AutofillGroup(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Icon(Icons.wind_power, size: AppSizes.iconXxl, color: Theme.of(context).colorScheme.primary),
                        AppSpacing.gapMd,
                        Text('UA GREEN', style: t.headlineSmall, textAlign: TextAlign.center),
                        Text('Адмінка сайту uagreen.co', style: t.bodyMedium, textAlign: TextAlign.center),
                        AppSpacing.gapXl,
                        if (_error != null) ...[FormBanner(_error!), AppSpacing.gapMd],
                        TextFormField(
                          controller: _email,
                          autofillHints: const [AutofillHints.email],
                          keyboardType: TextInputType.emailAddress,
                          decoration: const InputDecoration(labelText: 'Email', prefixIcon: Icon(Icons.alternate_email)),
                          validator: (v) => v!.contains('@') ? null : 'Введіть email',
                        ),
                        AppSpacing.gapMd,
                        TextFormField(
                          controller: _password,
                          obscureText: _hidden,
                          autofillHints: const [AutofillHints.password],
                          decoration: InputDecoration(
                            labelText: 'Пароль',
                            prefixIcon: const Icon(Icons.lock_outline),
                            suffixIcon: IconButton(
                              tooltip: _hidden ? 'Показати' : 'Сховати',
                              icon: Icon(_hidden ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                              onPressed: () => setState(() => _hidden = !_hidden),
                            ),
                          ),
                          validator: (v) => v!.isEmpty ? 'Введіть пароль' : null,
                          onFieldSubmitted: (_) => _submit(),
                        ),
                        AppSpacing.gapXl,
                        AppButton(label: 'Увійти', icon: Icons.login, isLoading: _busy, expand: true, onPressed: _submit),
                        AppSpacing.gapMd,
                        Text(
                          'Обліковий запис Господаря (erp.uagreen.co). Доступ до розділів дає роль, яку призначає адміністратор.',
                          style: t.bodySmall,
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Section switching from anywhere (dashboard tiles → a filtered list).
final nav = Nav();

class Nav extends ChangeNotifier {
  String key = browser.hashPath().split('?').first;

  /// One-shot arguments for the page being opened, e.g. {'status': 'New'}.
  Map<String, String> args = {
    ...Uri.splitQueryString(browser.hashPath().contains('?') ? browser.hashPath().split('?').last : ''),
  };

  void go(String key, [Map<String, String> args = const {}]) {
    this.key = key;
    this.args = {...args};
    sync();
    notifyListeners();
  }

  /// Writes the current section back into the address bar.
  void sync() => browser.setHashPath(args.isEmpty ? key : '$key?${Uri(queryParameters: args).query}');

  /// Reads and clears an argument (so a later visit opens unfiltered).
  String? take(String name) => args.remove(name);
}

class Shell extends StatefulWidget {
  const Shell({super.key});
  @override
  State<Shell> createState() => _ShellState();
}

class _ShellState extends State<Shell> {
  final _visible = sectionsFor(api.roles);
  Timer? _poll;
  int _openCount = 0; // re-keys the page on every navigation, even to the same section

  @override
  void initState() {
    super.initState();
    if (!_visible.any((s) => s.key == nav.key)) nav.key = _visible.first.key;
    nav.sync();
    nav.addListener(_onNav);
    summary.refresh();
    _poll = Timer.periodic(const Duration(minutes: 1), (_) => summary.refresh());
  }

  @override
  void dispose() {
    _poll?.cancel();
    nav.removeListener(_onNav);
    super.dispose();
  }

  void _onNav() => setState(() => _openCount++);

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= Breakpoints.tablet;
    final i = _visible.indexWhere((s) => s.key == nav.key).clamp(0, _visible.length - 1);
    final s = _visible[i];
    final theme = Theme.of(context);
    final menu = ListenableBuilder(
      listenable: summary,
      builder: (context, _) => NavigationDrawer(
        backgroundColor: context.palette.sidebarBg,
        selectedIndex: i,
        onDestinationSelected: (n) {
          if (!wide) Navigator.pop(context);
          nav.go(_visible[n].key);
        },
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.xl, AppSpacing.xl, AppSpacing.lg, AppSpacing.lg),
            child: Row(
              children: [
                Icon(Icons.wind_power, color: theme.colorScheme.primary),
                AppSpacing.hGapSm,
                Text('UA GREEN', style: theme.textTheme.titleLarge),
              ],
            ),
          ),
          for (final x in _visible)
            NavigationDrawerDestination(
              icon: _Badged(count: x.badge?.call(summary.data) ?? 0, child: Icon(x.icon)),
              label: Text(x.title),
            ),
        ],
      ),
    );
    return Scaffold(
      appBar: AppBar(
        title: Text(s.title),
        actions: [
          ListenableBuilder(
            listenable: summary,
            builder: (_, _) => Row(
              children: [
                if (summary.link('site') case final u?)
                  IconButton(tooltip: 'Відкрити сайт', icon: const Icon(Icons.public), onPressed: () => browser.openTab(u)),
                if (summary.link('erp') case final u?)
                  IconButton(tooltip: 'Господар (ERP)', icon: const Icon(Icons.business_center_outlined), onPressed: () => browser.openTab(u)),
                if (summary.link('oberig') case final u?)
                  IconButton(tooltip: 'Оберіг (портал)', icon: const Icon(Icons.monitor_heart_outlined), onPressed: () => browser.openTab(u)),
              ],
            ),
          ),
          ValueListenableBuilder(
            valueListenable: themeMode,
            builder: (context, mode, _) {
              final dark = mode == ThemeMode.dark ||
                  (mode == ThemeMode.system && MediaQuery.platformBrightnessOf(context) == Brightness.dark);
              return IconButton(
                tooltip: dark ? 'Світла тема' : 'Темна тема',
                icon: Icon(dark ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
                onPressed: () {
                  themeMode.value = dark ? ThemeMode.light : ThemeMode.dark;
                  browser.storageSet('uag.theme', dark ? 'light' : 'dark');
                },
              );
            },
          ),
          PopupMenuButton<void>(
            tooltip: 'Обліковий запис',
            icon: AppAvatar(name: '${api.user!['fullName']}'),
            itemBuilder: (_) => [
              PopupMenuItem(
                enabled: false,
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('${api.user!['fullName']}'),
                  subtitle: Text([api.user!['email'], ...api.roles.map((r) => siteRoles[r] ?? r)].join(' · ')),
                ),
              ),
              const PopupMenuDivider(),
              PopupMenuItem(
                onTap: api.logout,
                child: const ListTile(contentPadding: EdgeInsets.zero, leading: Icon(Icons.logout), title: Text('Вийти')),
              ),
            ],
          ),
          AppSpacing.hGapSm,
        ],
      ),
      drawer: wide ? null : menu,
      body: Row(
        children: [
          if (wide) SizedBox(width: AppSizes.sidebarWidth, child: menu),
          Expanded(
            child: KeyedSubtree(key: ValueKey('${s.key}#$_openCount'), child: s.build()),
          ),
        ],
      ),
    );
  }
}

class _Badged extends StatelessWidget {
  const _Badged({required this.count, required this.child});
  final int count;
  final Widget child;

  @override
  Widget build(BuildContext context) =>
      count == 0 ? child : Badge(label: Text(count > 99 ? '99+' : '$count'), child: child);
}
