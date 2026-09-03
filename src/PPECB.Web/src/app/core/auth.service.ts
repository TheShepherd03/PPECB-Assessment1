import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse } from './models';

const STORAGE_KEY = 'ppecb.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<AuthResponse | null>(this.restore());

  readonly currentUser = this.session.asReadonly();
  readonly isLoggedIn = computed(() => this.session() !== null && !this.isExpired(this.session()));
  readonly email = computed(() => this.session()?.email ?? '');

  async register(email: string, password: string, confirmPassword: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, {
        email,
        password,
        confirmPassword
      })
    );
    this.persist(response);
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
    );
    this.persist(response);
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    void this.router.navigate(['/login']);
  }

  /** The bearer token, or null when signed out or expired. */
  token(): string | null {
    const session = this.session();
    if (!session || this.isExpired(session)) {
      return null;
    }
    return session.token;
  }

  private persist(response: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this.session.set(response);
  }

  private restore(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as AuthResponse;
      // Drop a token that has already expired rather than sending it and getting a 401.
      return this.isExpired(parsed) ? null : parsed;
    } catch {
      // Corrupted storage should log the user out, not crash the app on boot.
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  }

  private isExpired(session: AuthResponse | null): boolean {
    if (!session) {
      return true;
    }
    return new Date(session.expiresAtUtc).getTime() <= Date.now();
  }
}
