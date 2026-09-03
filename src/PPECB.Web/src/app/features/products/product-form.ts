import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProductService } from '../../core/product.service';
import { CategoryService } from '../../core/category.service';
import { CategoryLookup } from '../../core/models';
import { describeError, fieldErrors } from '../../core/api-error';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-form.html'
})
export class ProductFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly products = inject(ProductService);
  private readonly categories = inject(CategoryService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly productId = signal<number | null>(null);
  readonly productCode = signal<string | null>(null);
  readonly imageUrl = signal<string | null>(null);
  readonly lookup = signal<CategoryLookup[]>([]);

  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  /**
   * Flipped on the first submit attempt so validation messages show for every field,
   * including any the user never focused.
   */
  readonly submitted = signal(false);

  private rowVersion: string | null = null;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    categoryId: [0, [Validators.required, Validators.min(1)]],
    price: [0, [Validators.required, Validators.min(0)]]
  });

  get isEdit(): boolean {
    return this.productId() !== null;
  }

  async ngOnInit(): Promise<void> {
    await this.loadLookup();

    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam || idParam === 'new') {
      return;
    }

    const id = Number(idParam);
    if (Number.isNaN(id)) {
      this.error.set('That product id is not valid.');
      return;
    }

    this.productId.set(id);
    await this.load(id);
  }

  private async loadLookup(): Promise<void> {
    try {
      this.lookup.set(await firstValueFrom(this.categories.getLookup()));
    } catch (err) {
      this.error.set(describeError(err));
    }
  }

  private async load(id: number): Promise<void> {
    this.loading.set(true);
    try {
      const product = await firstValueFrom(this.products.getById(id));
      this.rowVersion = product.rowVersion;
      this.productCode.set(product.productCode);
      this.imageUrl.set(product.imagePath ? `${environment.apiOrigin}${product.imagePath}` : null);
      this.form.patchValue({
        name: product.name,
        description: product.description ?? '',
        categoryId: product.categoryId,
        price: product.price
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
    const payload = {
      name: value.name,
      description: value.description?.trim() ? value.description : null,
      categoryId: Number(value.categoryId),
      price: Number(value.price)
    };

    try {
      const id = this.productId();
      if (id === null) {
        const created = await firstValueFrom(this.products.create(payload));
        // Stay on the form so the user can immediately attach an image.
        this.productId.set(created.productId);
        this.productCode.set(created.productCode);
        this.rowVersion = created.rowVersion;
        this.notice.set(`Product created with code ${created.productCode}. You can now upload an image.`);
        await this.router.navigate(['/products', created.productId], { replaceUrl: true });
      } else {
        const updated = await firstValueFrom(
          this.products.update(id, { ...payload, rowVersion: this.rowVersion })
        );
        this.rowVersion = updated.rowVersion;
        this.notice.set('Changes saved.');
      }
    } catch (err) {
      this.serverErrors.set(fieldErrors(err));
      this.error.set(describeError(err));
    } finally {
      this.submitting.set(false);
    }
  }

  async onImageSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const id = this.productId();

    if (!file || id === null) {
      return;
    }

    this.uploading.set(true);
    this.error.set(null);

    try {
      const updated = await firstValueFrom(this.products.uploadImage(id, file));
      this.rowVersion = updated.rowVersion;
      // Cache-bust so a replaced image is shown rather than the browser's copy.
      this.imageUrl.set(
        updated.imagePath ? `${environment.apiOrigin}${updated.imagePath}?v=${Date.now()}` : null
      );
      this.notice.set('Image uploaded.');
    } catch (err) {
      this.error.set(describeError(err));
    } finally {
      this.uploading.set(false);
      input.value = '';
    }
  }
}
