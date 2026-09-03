import { HttpErrorResponse } from '@angular/common/http';
import { ApiProblem } from './models';

/**
 * Turns an API failure into a message a user can act on. The API returns RFC 7807
 * problem documents, so field errors and plain details are handled separately.
 */
export function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Could not reach the server. Check that the API is running.';
    }

    const problem = error.error as ApiProblem | string | null;

    if (typeof problem === 'string' && problem.trim()) {
      return problem;
    }

    if (problem && typeof problem === 'object') {
      if (problem.errors) {
        const messages = Object.values(problem.errors).flat();
        if (messages.length) {
          return messages.join(' ');
        }
      }
      if (problem.detail) {
        return problem.detail;
      }
      if (problem.title) {
        return problem.title;
      }
    }

    if (error.status === 409) {
      return 'That record was changed by someone else. Reload and try again.';
    }
  }

  return 'Something went wrong. Please try again.';
}

/** Field-level messages, keyed by the field name the API reported. */
export function fieldErrors(error: unknown): Record<string, string> {
  const result: Record<string, string> = {};

  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ApiProblem | null;
    if (problem?.errors) {
      for (const [field, messages] of Object.entries(problem.errors)) {
        // Normalise casing so templates can look fields up predictably.
        result[field.charAt(0).toLowerCase() + field.slice(1)] = messages.join(' ');
      }
    }
  }

  return result;
}
