import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import 'browser.dart' as browser;

/// `--dart-define=API_HOST=https://admin.uagreen.co` (deploy.sh); empty = same origin.
const apiHost = String.fromEnvironment('API_HOST', defaultValue: kReleaseMode ? '' : 'http://localhost:5000');

String mediaUrl(String keyOrUrl) => keyOrUrl.startsWith('/') ? '$apiHost$keyOrUrl' : '$apiHost/media/$keyOrUrl';

class ApiError implements Exception {
  ApiError(this.message, [this.status]);
  final String message;
  final int? status;
  @override
  String toString() => message;
}

final api = Api();

/// JSON calls to `/api` with the Gospodar session (trimmed TTApp ApiClient + AuthService):
/// bearer on every call, one refresh on 401 then retry, logout when refresh is refused.
class Api extends ChangeNotifier {
  static const _refreshKey = 'uag.refresh_token';

  final _dio = Dio(
    BaseOptions(
      baseUrl: '$apiHost/api',
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 60),
    ),
  );
  bool booting = true;
  Map<String, dynamic>? user; // MeDto: id, email, fullName, isGospodarAdmin, roles
  String? _access;
  Future<bool>? _refreshing;

  List<String> get roles => [...?(user?['roles'] as List?)].cast<String>();

  Api() {
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (o, h) {
          if (_access != null && !o.path.startsWith('/auth/')) o.headers['Authorization'] = 'Bearer $_access';
          h.next(o);
        },
        onError: (e, h) async {
          final o = e.requestOptions;
          if (e.response?.statusCode != 401 || o.path.startsWith('/auth/')) return h.next(e);
          if (o.extra['retried'] == true) {
            logout(); // a fresh token was refused too
            return h.next(e);
          }
          try {
            if (!await _refresh()) return h.next(e);
            o.extra['retried'] = true;
            if (o.data is FormData) o.data = (o.data as FormData).clone(); // finalized by the first send
            h.resolve(await _dio.fetch(o));
          } on DioException catch (e2) {
            h.next(e2);
          } on ApiError {
            h.next(e);
          }
        },
      ),
    );
  }

  Future<void> boot() async {
    try {
      if (browser.storageGet(_refreshKey) != null) await _refresh();
    } catch (_) {
      // API down: show the login form; the stored token is kept for the next reload.
    }
    booting = false;
    notifyListeners();
  }

  Future<void> login(String email, String password) async =>
      _apply(await post('/auth/login', {'email': email, 'password': password}));

  void logout() {
    user = null;
    _access = null;
    browser.storageSet(_refreshKey, null);
    notifyListeners();
  }

  void _apply(dynamic auth) {
    _access = auth['accessToken'];
    browser.storageSet(_refreshKey, auth['refreshToken']);
    user = auth['user'];
    notifyListeners();
  }

  /// Single-flight: parallel 401s share one refresh (Gospodar rotates refresh tokens).
  Future<bool> _refresh() => _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<bool> _doRefresh() async {
    final token = browser.storageGet(_refreshKey);
    if (token == null) return false;
    try {
      _apply(await post('/auth/refresh', {'refreshToken': token}));
      return true;
    } on ApiError catch (e) {
      if (e.status != 401 && e.status != 403) rethrow;
      logout();
      return false;
    }
  }

  Future<dynamic> get(String path, [Map<String, dynamic>? query]) =>
      _call(() => _dio.get(path, queryParameters: query));
  Future<dynamic> post(String path, Object? body) => _call(() => _dio.post(path, data: body));
  Future<dynamic> put(String path, Object? body) => _call(() => _dio.put(path, data: body));
  Future<dynamic> patch(String path, Object? body) => _call(() => _dio.patch(path, data: body));
  Future<dynamic> delete(String path) => _call(() => _dio.delete(path));

  Future<Uint8List> bytes(String path) async => Uint8List.fromList(
    await _call(() => _dio.get<List<int>>(path, options: Options(responseType: ResponseType.bytes))),
  );

  /// `POST /api/admin/media` → media key.
  Future<String> upload(String folder, browser.PickedFile f) async =>
      (await _call(
            () => _dio.post(
              '/admin/media',
              data: FormData.fromMap({
                'folder': folder,
                'file': MultipartFile.fromBytes(
                  f.bytes,
                  filename: f.name,
                  contentType: DioMediaType.parse(f.type.isEmpty ? 'application/octet-stream' : f.type),
                ),
              }),
            ),
          ))['key']
          as String;

  Future<dynamic> _call(Future<Response> Function() fn) async {
    try {
      return (await fn()).data;
    } on DioException catch (e) {
      final r = e.response;
      final body = r?.data;
      throw ApiError(
        body is Map && body['message'] is String
            ? body['message'] as String
            : r == null
            ? "Немає зв'язку з сервером"
            : 'Помилка ${r.statusCode}',
        r?.statusCode,
      );
    }
  }
}
