import 'dart:math';

import 'package:flutter/material.dart';

import 'api.dart';
import 'browser.dart' as browser;
import 'form.dart';

/// Switches the drawer to the section with this title (set by the Shell in main.dart).
void Function(String title)? openSection;

const _sources = {
  'Direct': 'Прямий',
  'Search': 'Пошукові системи',
  'Referral': 'Перехід за посиланнями з інших сайтів',
  'Social': 'Соцмережі',
  'Ads': 'Реклама',
  'Email': 'Пошта',
  'Other': 'Інше',
};
const _forms = {'all': 'Усі форми', 'contacts': 'Контактна форма', 'applications': 'Відгуки на вакансії'};

/// Site traffic from the site's own counter (`GET /api/admin/stats`).
class StatsPage extends StatefulWidget {
  const StatsPage({super.key});
  @override
  State<StatsPage> createState() => _StatsPageState();
}

class _StatsPageState extends State<StatsPage> {
  static final _today = DateUtils.dateOnly(DateTime.now());
  var _range = DateTimeRange(start: DateUtils.addDaysToDate(_today, -7), end: _today);
  late Future<J> _data = _load();
  String _form = 'all';

  Future<J> _load() async {
    String d(DateTime x) => x.toIso8601String().substring(0, 10);
    return await api.get('/admin/stats', {'from': d(_range.start), 'to': d(_range.end)}) as J;
  }

  void _reload() => setState(() => _data = _load());

  Future<void> _pickRange() async {
    final r = await showDateRangePicker(
      context: context,
      firstDate: DateUtils.addDaysToDate(_today, -365),
      lastDate: _today,
      initialDateRange: _range,
    );
    if (r == null) return;
    _range = r;
    _reload();
  }

  Future<void> _setDisabled(bool off) async {
    try {
      await api.put('/admin/settings', {'stats_disabled': off ? '1' : ''}); // empty value deletes the key
      _reload();
    } catch (e) {
      if (mounted) toast(context, '$e');
    }
  }

  @override
  Widget build(BuildContext context) => FutureBuilder(
    future: _data,
    builder: (context, s) {
      if (s.hasError) return Center(child: Text('${s.error}'));
      if (!s.hasData) return const Center(child: CircularProgressIndicator());
      return _body(context, s.data!, s.connectionState == ConnectionState.waiting);
    },
  );

  Widget _body(BuildContext context, J d, bool loading) {
    final t = Theme.of(context);
    final loc = MaterialLocalizations.of(context);
    final days = (d['days'] as List).cast<J>();
    final labels = [for (final x in days) loc.formatShortMonthDay(DateTime.parse(x['date']))];
    final cur = d['current'] as J, prev = d['previous'] as J;
    final sources = d['sources'] as J;
    final sessions = sources.values.fold<num>(0, (a, b) => a + (b as num));
    final countries = (d['countries'] as List).cast<J>();
    final known = countries.fold<num>(0, (a, c) => a + c['count']);
    final pages = (d['pages'] as List).cast<J>();
    final siteUrl = d['siteUrl'] as String;
    final disabled = d['disabled'] == true;
    num forms(J x) => switch (_form) {
      'contacts' => x['contacts'],
      'applications' => x['applications'],
      _ => x['contacts'] + x['applications'],
    };
    final metrics = [
      _metric(
        context,
        'Сесії',
        'Візити на сайт. Новий візит — після 30 хв без переглядів.',
        cur['sessions'],
        prev['sessions'],
      ),
      _metric(
        context,
        'Перегляди сторінок',
        'Скільки разів відкрили сторінки сайту.',
        cur['pageViews'],
        prev['pageViews'],
      ),
      _metric(
        context,
        'Унікальні відвідувачі',
        'Різні браузери, з яких заходили на сайт за період.',
        cur['visitors'],
        prev['visitors'],
      ),
      _metric(
        context,
        'Відповіді з форм',
        'Контактні запити та відгуки на вакансії з сайту.',
        cur['forms'],
        prev['forms'],
      ),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1200),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 12,
                  runSpacing: 8,
                  children: [
                    if (disabled)
                      Text('Збір статистики вимкнено', style: TextStyle(color: t.colorScheme.error))
                    else
                      const Text('Порівняння — з попереднім періодом такої ж довжини'),
                    OutlinedButton.icon(
                      onPressed: _pickRange,
                      icon: const Icon(Icons.calendar_month),
                      label: Text(
                        '${fmtDate(_range.start.toIso8601String(), time: false)} – '
                        '${fmtDate(_range.end.toIso8601String(), time: false)}',
                      ),
                    ),
                  ],
                ),
                SizedBox(height: 4, child: loading ? const LinearProgressIndicator() : null),
                const SizedBox(height: 8),
                Card(
                  margin: EdgeInsets.zero,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _head(
                          context,
                          'Огляд',
                          OutlinedButton(
                            onPressed: () => openSection?.call('Контактні запити'),
                            child: const Text('Відкрити відповіді з форм'),
                          ),
                        ),
                        DecoratedBox(
                          decoration: BoxDecoration(
                            border: Border.all(color: t.colorScheme.outlineVariant),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: LayoutBuilder(
                            builder: (context, c) {
                              final w = (c.maxWidth / (c.maxWidth >= 720 ? 4 : 2)).floorToDouble();
                              return Wrap(
                                children: [for (final m in metrics) SizedBox(width: w, child: m)],
                              );
                            },
                          ),
                        ),
                        _head(context, 'Сесії'),
                        _chart([for (final x in days) x['sessions']], labels),
                        _head(context, 'Перегляди сторінок'),
                        _chart([for (final x in days) x['pageViews']], labels),
                        _head(
                          context,
                          'Відповіді з форм',
                          DropdownButton<String>(
                            value: _form,
                            items: [
                              for (final e in _forms.entries) DropdownMenuItem(value: e.key, child: Text(e.value)),
                            ],
                            onChanged: (v) => setState(() => _form = v!),
                          ),
                        ),
                        _chart([for (final x in days) forms(x)], labels, bars: true),
                        _head(context, 'Джерела трафіку'),
                        for (final e in _sources.entries)
                          _share(e.value, sessions == 0 ? 0 : (sources[e.key] ?? 0) / sessions),
                        _head(context, 'Топ-5 країн'),
                        if (countries.isEmpty) const Text('Ще немає даних'),
                        for (final c in countries.take(5)) _share(browser.countryName(c['key']), c['count'] / known),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            onPressed: () => browser.openUrl('https://db-ip.com'),
                            child: const Text('IP Geolocation by DB-IP', style: TextStyle(fontSize: 12)),
                          ),
                        ),
                        _head(context, 'Топ-5 популярних сторінок'),
                        _pagesTable(context, pages, siteUrl),
                        _head(context, 'Налаштування статистики'),
                        SwitchListTile(
                          contentPadding: EdgeInsets.zero,
                          controlAffinity: ListTileControlAffinity.leading,
                          value: disabled,
                          onChanged: api.roles.contains('Admin') || api.roles.contains('Editor') ? _setDisabled : null,
                          title: const Text('Вимкнути статистику сайту'),
                          subtitle: const Text('Сайт перестане рахувати перегляди; вже зібрані дані залишаться.'),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

Widget _head(BuildContext context, String title, [Widget? trailing]) => Padding(
  padding: const EdgeInsets.only(top: 32, bottom: 16),
  child: Row(
    children: [
      Expanded(
        child: Text(
          title.toUpperCase(),
          style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: .5),
        ),
      ),
      ?trailing,
    ],
  ),
);

Widget _metric(BuildContext context, String label, String help, num cur, num prev) {
  final t = Theme.of(context);
  final pct = prev == 0 ? null : (cur - prev) * 100 / prev;
  final color = pct == null
      ? t.colorScheme.outline
      : pct >= 0
      ? Colors.green.shade700
      : t.colorScheme.error;
  return Padding(
    padding: const EdgeInsets.symmetric(vertical: 24, horizontal: 8),
    child: Column(
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Flexible(
              child: Text(
                label,
                style: TextStyle(color: t.colorScheme.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
            ),
            const SizedBox(width: 4),
            Tooltip(
              message: help,
              child: Icon(Icons.help, size: 16, color: t.colorScheme.outline),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text('$cur', style: t.textTheme.headlineMedium),
        const SizedBox(height: 8),
        Tooltip(
          message: pct == null ? 'За попередній період даних немає' : 'Попередній період: $prev',
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (pct != null) Icon(pct >= 0 ? Icons.north_east : Icons.south_east, size: 14, color: color),
              const SizedBox(width: 4),
              Text(
                pct == null ? '—' : '${pct >= 0 ? '+' : ''}${pct.toStringAsFixed(2)}%',
                style: TextStyle(color: color, fontSize: 13),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

/// Label + percentage + bar (traffic sources, countries).
Widget _share(String label, num share) => Padding(
  padding: const EdgeInsets.only(bottom: 16),
  child: Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          Expanded(child: Text(label)),
          Text('${(share * 100).toStringAsFixed(1)}%'),
        ],
      ),
      const SizedBox(height: 6),
      LinearProgressIndicator(value: share.toDouble(), minHeight: 8, borderRadius: BorderRadius.circular(4)),
    ],
  ),
);

Widget _pagesTable(BuildContext context, List<J> pages, String siteUrl) {
  final cs = Theme.of(context).colorScheme;
  final head = TextStyle(color: cs.onSurfaceVariant, fontSize: 13);
  Widget row(Widget a, Widget b, Color? bg) => Container(
    color: bg,
    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
    child: Row(
      children: [
        Expanded(flex: 3, child: a),
        Expanded(flex: 2, child: b),
      ],
    ),
  );
  if (pages.isEmpty) return const Text('Ще немає даних');
  return Column(
    children: [
      row(Text('СТОРІНКИ', style: head), Text('ПЕРЕГЛЯДИ СТОРІНОК', style: head), cs.surfaceContainerHigh),
      for (final (i, p) in pages.indexed)
        row(
          Align(
            alignment: Alignment.centerLeft,
            child: InkWell(
              onTap: () => browser.openUrl('$siteUrl${p['key']}'),
              child: Text(p['key'], style: TextStyle(color: cs.primary)),
            ),
          ),
          Text('${p['count']}'),
          i.isOdd ? cs.surfaceContainerLow : null,
        ),
    ],
  );
}

Widget _chart(List<num> values, List<String> labels, {bool bars = false}) => Builder(
  builder: (context) => SizedBox(
    height: 300,
    child: CustomPaint(
      size: Size.infinite,
      painter: _ChartPainter(values, labels, bars, Theme.of(context).colorScheme),
    ),
  ),
);

/// Line (smooth, filled) or bar chart with a 0-based y axis.
/// ponytail: no hover tooltips — add a MouseRegion + nearest-point label if exact daily numbers are needed.
class _ChartPainter extends CustomPainter {
  _ChartPainter(this.values, this.labels, this.bars, this.cs);
  final List<num> values;
  final List<String> labels;
  final bool bars;
  final ColorScheme cs;

  static const left = 40.0, right = 28.0, top = 8.0, bottom = 28.0;

  /// 1/2/5×10ⁿ step giving about seven grid lines, never below 1 (counts are integers).
  static double _step(double maxV) {
    final raw = max(maxV, 4) / 7;
    final mag = pow(10, (log(raw) / ln10).floor()).toDouble();
    return max(1, [1, 2, 5, 10].map((k) => k * mag).firstWhere((s) => s >= raw));
  }

  void _text(Canvas canvas, String s, Offset at, Alignment align) {
    final tp = TextPainter(
      text: TextSpan(
        text: s,
        style: TextStyle(color: cs.onSurfaceVariant, fontSize: 12),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    tp.paint(canvas, at - Offset(tp.width * (align.x + 1) / 2, tp.height * (align.y + 1) / 2));
  }

  @override
  void paint(Canvas canvas, Size size) {
    final n = values.length;
    if (n == 0) return;
    final maxV = values.fold<num>(0, max).toDouble();
    final step = _step(maxV);
    final yMax = max(step, (maxV * 1.05 / step).ceil() * step);
    final w = size.width - left - right, h = size.height - top - bottom;
    double y(num v) => top + h - v / yMax * h;
    final slot = w / (bars ? n : max(1, n - 1));
    double x(int i) => left + (bars ? slot * (i + .5) : (n == 1 ? w / 2 : slot * i));

    final grid = Paint()..color = cs.outlineVariant.withValues(alpha: .6);
    for (var v = 0.0; v <= yMax + 1e-9; v += step) {
      canvas.drawLine(Offset(left, y(v)), Offset(left + w, y(v)), grid);
      _text(canvas, '${v.round()}', Offset(left - 6, y(v)), Alignment.centerRight);
    }
    final every = max(1, (n * 72 / w).ceil()); // skip labels that would overlap
    for (var i = 0; i < n; i += every) {
      if (!bars) canvas.drawLine(Offset(x(i), top), Offset(x(i), top + h), grid);
      _text(canvas, labels[i], Offset(x(i), top + h + 6), Alignment.topCenter);
    }
    canvas.drawLine(Offset(left, y(0)), Offset(left + w, y(0)), Paint()..color = cs.outline);

    final paint = Paint()..color = cs.primary;
    if (bars) {
      final bw = min(slot * .5, 48.0);
      for (var i = 0; i < n; i++) {
        if (values[i] == 0) continue;
        final r = Rect.fromLTRB(x(i) - bw / 2, y(values[i]), x(i) + bw / 2, y(0));
        canvas.drawRRect(
          RRect.fromRectAndCorners(r, topLeft: const Radius.circular(3), topRight: const Radius.circular(3)),
          paint,
        );
      }
      return;
    }
    // Catmull-Rom → cubic Bézier; control points clamped to the plot so the curve never dips below 0.
    final pts = [for (var i = 0; i < n; i++) Offset(x(i), y(values[i]))];
    Offset clamp(Offset o) => Offset(o.dx, o.dy.clamp(top, y(0)));
    final line = Path()..moveTo(pts[0].dx, pts[0].dy);
    for (var i = 0; i < n - 1; i++) {
      final p0 = pts[max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[min(i + 2, n - 1)];
      final c1 = clamp(p1 + (p2 - p0) / 6), c2 = clamp(p2 - (p3 - p1) / 6);
      line.cubicTo(c1.dx, c1.dy, c2.dx, c2.dy, p2.dx, p2.dy);
    }
    final area = Path.from(line)
      ..lineTo(pts.last.dx, y(0))
      ..lineTo(pts.first.dx, y(0))
      ..close();
    canvas.drawPath(area, Paint()..color = cs.primary.withValues(alpha: .08));
    canvas.drawPath(
      line,
      Paint()
        ..color = cs.primary
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.5,
    );
    for (final p in pts) {
      canvas.drawCircle(p, 4, paint);
    }
  }

  @override
  bool shouldRepaint(_ChartPainter o) => o.values != values || o.labels != labels || o.cs != cs || o.bars != bars;
}
