import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CategoryService } from '../../core/category.service';
import { Category, PagedResult } from '../../core/models';
import { describeError } from '../../core/api-error';

@Component({
  selector: 'app-category-list',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './category-list.html'
})
export class CategoryListComponent implements OnInit {
  private readonly categories = inject(CategoryService);

  readonly page = signal<PagedResult<Category> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly search = signal('');

  private currentPage = 1;

  ngOnInit(): void {
    void this.load(1);
  }

  async load(pageNumber: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const result = await firstValueFrom(
        this.categories.getPaged(pageNumber, 10, this.search())
      );
      this.page.set(result);
      this.currentPage = result.pageNumber;
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.loading.set(false);
    }
  }

  applySearch(): void {
    // A new search always restarts at page one, otherwise the user can land on an
    // out-of-range page and see an empty table.
    void this.load(1);
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
}
