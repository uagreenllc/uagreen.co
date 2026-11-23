// No imports: unit-tested on the VM (test/slug_test.dart).

/// Same rule as Slug.cs; `_` is there because some Weblium slugs end in `_1`.
final slugPattern = RegExp(r'^[a-z0-9]+(?:[-_][a-z0-9]+)*$');

const _lat = {
  'а': 'a', 'б': 'b', 'в': 'v', 'г': 'g', 'ґ': 'g', 'д': 'd', 'е': 'e', 'є': 'ye', 'ж': 'zh', 'з': 'z', 'и': 'i', //
  'і': 'i', 'ї': 'yi', 'й': 'j', 'к': 'k', 'л': 'l', 'м': 'm', 'н': 'n', 'о': 'o', 'п': 'p', 'р': 'r', 'с': 's',
  'т': 't', 'у': 'u', 'ф': 'f', 'х': 'h', 'ц': 'c', 'ч': 'ch', 'ш': 'sh', 'щ': 'sh', 'ь': '', 'ю': 'yu', 'я': 'ya',
  'ы': 'y', 'э': 'e', 'ё': 'yo', 'ъ': '', "'": '', '’': '', 'ʼ': '',
};

/// Post title → URL slug, transliterated the way Weblium did it ("Анталії" → "antaliyi").
String slugify(String title) {
  final s = title
      .toLowerCase()
      .split('')
      .map((c) => _lat[c] ?? c)
      .join()
      .replaceAll(RegExp(r'[^a-z0-9]+'), '-')
      .replaceAll(RegExp(r'^-|-$'), '');
  return s.length <= 100 ? s : s.substring(0, 100).replaceAll(RegExp(r'-[^-]*$'), '');
}
