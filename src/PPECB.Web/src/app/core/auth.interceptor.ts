import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

/**
 * Attaches the bearer token to API calls and signs the user out on a 401.
 * The token is only ever sent to our own API origin, never to third parties.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  const isApiCall = request.url.startsWith(environment.apiUrl);
  const authorised =
    token && isApiCall
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorised).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && isApiCall) {
        auth.logout();
      }
      return throwError(() => error);
    })
  );
};
