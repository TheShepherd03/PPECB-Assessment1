import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ExcelImportResult, PagedResult, Product } from './models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/products`;

  getPaged(
    pageNumber: number,
    pageSize: number,
    search?: string,
    categoryId?: number | null
  ): Observable<PagedResult<Product>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    if (search?.trim()) {
      params = params.set('search', search.trim());
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }

    return this.http.get<PagedResult<Product>>(this.baseUrl, { params });
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/${id}`);
  }

  create(payload: {
    name: string;
    description: string | null;
    categoryId: number;
    price: number;
  }): Observable<Product> {
    return this.http.post<Product>(this.baseUrl, payload);
  }

  update(
    id: number,
    payload: {
      name: string;
      description: string | null;
      categoryId: number;
      price: number;
      rowVersion: string | null;
    }
  ): Observable<Product> {
    return this.http.put<Product>(`${this.baseUrl}/${id}`, payload);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  uploadImage(id: number, file: File): Observable<Product> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<Product>(`${this.baseUrl}/${id}/image`, form);
  }

  importExcel(file: File): Observable<ExcelImportResult> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ExcelImportResult>(`${this.baseUrl}/import`, form);
  }

  /** Downloads are fetched as a blob so the bearer token is still applied. */
  exportExcel(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export`, { responseType: 'blob' });
  }

  downloadImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/import-template`, { responseType: 'blob' });
  }
}
