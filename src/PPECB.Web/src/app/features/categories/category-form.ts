import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CategoryService } from '../../core/category.service';
import { describeError, fieldErrors } from '../../core/api-error';

@Component({
  selector: 'app-category-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './category-form.html'
})
export class CategoryFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly categories = inject(CategoryService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly categoryId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});

  /**
   * Flipped on the first submit attempt so validation messages show for every field,
   * including any the user never focused.
   */
  readonly submitted = signal(false);

  private rowVersion: string | null = null;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    // Mirrors the server rule so the user gets feedback before submitting.
    categoryCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}[0-9]{3}$/)]],
    isActive: [true]
  });

  get isEdit(): boolean {
    return this.categoryId() !== null;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam || idParam === 'new') {
      return;
    }

    const id = Number(idParam);
    if (Number.isNaN(id)) {
      this.error.set('That category id is not valid.');
      return;
    }

    this.categoryId.set(id);
    void this.load(id);
  }

  private async load(id: number): Promise<void> {
    this.loading.set(true);
    try {
      const category = await firstValueFrom(this.categories.getById(id));
      this.rowVersion = category.rowVersion;
      this.form.patchValue({
        name: category.name,
        categoryCode: category.categoryCode,
        isActive: category.isActive
      });
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async submit(): Promise<void> {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.serverErrors.set({});

    const value = this.form.getRawValue();

    try {
      const id = this.categoryId();
      if (id === null) {
        await firstValueFrom(this.categories.create(value));
      } else {
        await firstValueFrom(
          this.categories.update(id, { ...value, rowVersion: this.rowVersion })
        );
      }
      await this.router.navigateByUrl('/categories');
    } catch (err) {
      this.serverErrors.set(fieldErrors(err));
      this.error.set(describeError(err));
    } finally {
      this.submitting.set(false);
    }
  }
}
