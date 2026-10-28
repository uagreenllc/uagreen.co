import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'api.dart';
import 'pages.dart';

void main() {
  runApp(const AdminApp());
  api.boot();
}

class Section {
  const Section(this.title, this.icon, this.roles, this.build);
  final String title;
  final IconData icon;

  /// Any of these (or Admin) — mirrors the backend policies in SiteRoles.cs.
  final List<String> roles;
  final Widget Function() build;
}

final sections = [
  Section('Замовлення', Icons.receipt_long, ['Sales'], () => const OrdersPage()),
  Section('Товари', Icons.inventory_2, ['Sales'], productsPage),
  Section('Категорії', Icons.category, ['Sales'], categoriesPage),
  Section('Контактні запити', Icons.mail, ['Sales', 'Editor'], contactsPage),
  Section('Блог', Icons.article, ['Editor'], blogPage),
  Section('Контент сторінок', Icons.web, ['Editor'], contentPage),
  Section('Налаштування', Icons.settings, ['Editor'], () => const SettingsPage()),
  Section('Вакансії', Icons.work, ['Hr'], vacanciesPage),
  Section('Відгуки на вакансії', Icons.badge, ['Hr'], applicationsPage),
  Section('Користувачі', Icons.manage_accounts, ['Admin'], () => const UsersPage()),
];

List<Section> sectionsFor(List<String> roles) =>
    sections.where((s) => roles.contains('Admin') || s.roles.any(roles.contains)).toList();

class AdminApp extends StatelessWidget {
  const AdminApp({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'UA GREEN — адмінка',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(colorSchemeSeed: const Color(0xFF576E4F)),
    locale: const Locale('uk'),
    supportedLocales: const [Locale('uk')],
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    home: ListenableBuilder(
      listenable: api,
      builder: (_, _) => api.booting
          ? const Scaffold(body: Center(child: CircularProgressIndicator()))
          : api.user == null
          ? const LoginPage()
          : Shell(key: ValueKey(api.user!['id'])),
    ),
  );
}

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});
  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _email = TextEditingController(), _password = TextEditingController();
  String? _error;
  bool _busy = false;

  Future<void> _submit() async {
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
  Widget build(BuildContext context) => Scaffold(
    body: Center(
      child: SizedBox(
        width: 360,
        child: AutofillGroup(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('UA GREEN — адмінка', style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              const Text('Вхід з обліковим записом ERP (erp.uagreen.co)', textAlign: TextAlign.center),
              const SizedBox(height: 24),
              TextField(
                controller: _email,
                autofillHints: const [AutofillHints.email],
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(labelText: 'Email', border: OutlineInputBorder()),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _password,
                obscureText: true,
                autofillHints: const [AutofillHints.password],
                decoration: const InputDecoration(labelText: 'Пароль', border: OutlineInputBorder()),
                onSubmitted: (_) => _submit(),
              ),
              const SizedBox(height: 16),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ),
              FilledButton(onPressed: _busy ? null : _submit, child: const Text('Увійти')),
            ],
          ),
        ),
      ),
    ),
  );
}

class Shell extends StatefulWidget {
  const Shell({super.key});
  @override
  State<Shell> createState() => _ShellState();
}

class _ShellState extends State<Shell> {
  final _visible = sectionsFor(api.roles);
  int _i = 0;

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= 900;
    final s = _visible[_i];
    final nav = NavigationDrawer(
      selectedIndex: _i,
      onDestinationSelected: (i) {
        setState(() => _i = i);
        if (!wide) Navigator.pop(context);
      },
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(28, 20, 16, 12),
          child: Text('UA GREEN', style: Theme.of(context).textTheme.titleLarge),
        ),
        for (final s in _visible) NavigationDrawerDestination(icon: Icon(s.icon), label: Text(s.title)),
      ],
    );
    return Scaffold(
      appBar: AppBar(
        title: Text(s.title),
        actions: [
          Text('${api.user!['fullName']}'),
          IconButton(tooltip: 'Вийти', onPressed: api.logout, icon: const Icon(Icons.logout)),
          const SizedBox(width: 8),
        ],
      ),
      drawer: wide ? null : nav,
      body: Row(
        children: [
          if (wide) SizedBox(width: 260, child: nav),
          Expanded(
            child: KeyedSubtree(key: ValueKey(_i), child: s.build()),
          ),
        ],
      ),
    );
  }
}
