import 'package:flutter_test/flutter_test.dart';
import 'package:uagreen_admin/slug.dart';

void main() {
  test('slugify gives the slugs Weblium made', () {
    expect(
      slugify('UA Green на COP31 в Анталії: знайомства, презентації та нові партнерства'),
      'ua-green-na-cop31-v-antaliyi-znajomstva-prezentaciyi-ta-novi-partnerstva',
    );
    expect(
      slugify('Що краще: сонячні панелі чи вітроелектростанції?'),
      'sho-krashe-sonyachni-paneli-chi-vitroelektrostanciyi',
    );
    expect(slugify('Українські стартапи підкорюють Європу'), 'ukrayinski-startapi-pidkoryuyut-yevropu');
    expect(slugify("П'ять  кроків — до мети!"), 'pyat-krokiv-do-meti');
    expect(slugify('а' * 150).length, lessThanOrEqualTo(100));
    expect(slugPattern.hasMatch('sonce-viter-i-batareyi_1'), isTrue);
    expect(slugPattern.hasMatch('Bad slug'), isFalse);
  });
}
