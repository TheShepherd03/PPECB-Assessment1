import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProductService } from '../../core/product.service';
import { CategoryService } from '../../core/category.service';
import { CategoryLookup, ExcelImportResult, PagedResult, Product } from '../../core/models';
import { describeError } from '../../core/api-error';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-product-list',
  imports: [FormsModule, RouterLink, DatePipe, CurrencyPipe],
  templateUrl: './product-list.html'
})
export class ProductListComponent implements OnInit {
  private readonly products = inject(ProductService);
  private readonly categories = inject(CategoryService);

  readonly page = signal<PagedResult<Product> | null>(null);
  readonly lookup = signal<CategoryLookup[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly importResult = signal<ExcelImportResult | null>(null);

  readonly search = signal('');
  readonly categoryFilter = signal<number | null>(null);

  private currentPage = 1;

  ngOnInit(): void {
    void this.loadLookup();
    void this.load(1);
  }

  private async loadLookup(): Promise<void> {
    try {
      this.lookup.set(await firstValueFrom(this.categories.getLookup()));
    } catch {
      // A failed lookup only disables filtering; the list itself still works.
      this.lookup.set([]);
    }
  }

  async load(pageNumber: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const result = await firstValueFrom(
        this.products.getPaged(pageNumber, 10, this.search(), this.categoryFilter())
      );
      this.page.set(result);
      this.currentPage = result.pageNumber;
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.loading.set(false);
    }
  }

  applyFilters(): void {
    void this.load(1);
  }

  onCategoryFilterChange(value: string): void {
    this.categoryFilter.set(value ? Number(value) : null);
    this.applyFilters();
  }

  previous(): void {
    if (this.page()?.hasPreviousPage) {
      void this.load(this.currentPage - 1);
    }
  }

  next(): void {
    if (this.page()?.hasNextPage) {
      void this.load(this.currentPage + 1);
    }
  }

  imageUrl(product: Product): string | null {
    return product.imagePath ? `${environment.apiOrigin}${product.imagePath}` : null;
  }

  async remove(product: Product): Promise<void> {
    const confirmed = confirm(`Delete "${product.name}" (${product.productCode})? This cannot be undone.`);
    if (!confirmed) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      await firstValueFrom(this.products.delete(product.productId));
      this.notice.set(`Deleted ${product.productCode}.`);

      // Stepping back a page avoids landing on an empty last page after the delete.
      const result = this.page();
      const wasLastItemOnPage = result?.items.length === 1 && result.hasPreviousPage;
      await this.load(wasLastItemOnPage ? this.currentPage - 1 : this.currentPage);
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.busy.set(false);
    }
  }

  async exportExcel(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      const blob = await firstValueFrom(this.products.exportExcel());
      this.saveBlob(blob, `products-${new Date().toISOString().slice(0, 10)}.xlsx`);
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.busy.set(false);
    }
  }

  async downloadTemplate(): Promise<void> {
    this.busy.set(true);
    try {
      const blob = await firstValueFrom(this.products.downloadImportTemplate());
      this.saveBlob(blob, 'product-import-template.xlsx');
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.busy.set(false);
    }
  }

  async onImportFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.importResult.set(null);

    try {
      const result = await firstValueFrom(this.products.importExcel(file));
      this.importResult.set(result);
      this.notice.set(`Imported ${result.productsImported} product(s).`);
      await this.load(1);
    } catch (err) {
      // A rejected import returns the same result shape on a 400, so show its rows.
      const body = (err as { error?: ExcelImportResult })?.error;
      if (body && typeof body === 'object' && 'errors' in body && Array.isArray(body.errors)) {
        this.importResult.set(body);
      }
      this.error.set(describeError(err));
    } finally {
      this.busy.set(false);
      // Reset so selecting the same file again still fires a change event.
      input.value = '';
    }
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
