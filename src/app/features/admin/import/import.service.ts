import { Injectable } from '@angular/core';
import { ImportSummary } from './import.models';

@Injectable({ providedIn: 'root' })
export class ImportService {
  validateLocalCsv(file: File, dryRun: boolean): ImportSummary {
    return {
      sourceName: file.name,
      dryRun,
      totalRows: 0,
      loadedRows: 0,
      errorRows: 0,
    };
  }
}
