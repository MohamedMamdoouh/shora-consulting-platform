import { describe, expect, it } from 'vitest';
import { buildReceiptUploadFormData } from './receipt-upload-form-data.util';

describe('buildReceiptUploadFormData', () => {
  it('includes the receipt image, payment method, and trimmed sender reference', () => {
    const image = new File(['receipt'], 'receipt.jpg', { type: 'image/jpeg' });

    const formData = buildReceiptUploadFormData(image, 'VodafoneCash', '  01012345678  ');

    expect(formData.get('image')).toBe(image);
    expect(formData.get('method')).toBe('VodafoneCash');
    expect(formData.get('senderReference')).toBe('01012345678');
  });

  it('omits sender reference when it is blank or missing', () => {
    const image = new File(['receipt'], 'receipt.jpg', { type: 'image/jpeg' });

    expect(buildReceiptUploadFormData(image, 'InstaPay', '   ').has('senderReference')).toBe(false);
    expect(buildReceiptUploadFormData(image, 'InstaPay', null).has('senderReference')).toBe(false);
  });
});
