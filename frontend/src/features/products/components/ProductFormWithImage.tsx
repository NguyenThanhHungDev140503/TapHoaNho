import { Form, Input, InputNumber, Upload, Button, message, Image, Alert } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { useState, useEffect } from 'react';
import { uploadImageToImageKit, deleteImageKitFile } from '../api/imageKitService';
import { DropDownWithFilter } from '../../../components/common/DropDownWithFilter';
import { categoryApiService } from '../../categories/api';
import { supplierApiService } from '../../suppliers/api';
import type { CategoryEntity } from '../../categories/types/entity';
import type { SupplierEntity } from '../../suppliers/types/entity';
import type { DropDownWithFilterOption } from '../../../components/common/DropDownWithFilter';
import type { CreateProductRequest, UpdateProductRequest } from '../types/api';
import type { ProductEntity } from '../types/entity';

interface ProductFormWithImageProps {
  mode: 'create' | 'update';
  onSubmit: (values: CreateProductRequest | UpdateProductRequest) => Promise<void>;
  onCancel: () => void;
  loading: boolean;
  errorMessage: string | null;
  onClearError: () => void;
  initialValues?: Partial<CreateProductRequest | UpdateProductRequest>;
  record?: ProductEntity;
}

async function fetchCategoryOptions(keyword: string): Promise<DropDownWithFilterOption[]> {
  const paged = await categoryApiService.getPaginated({
    search: keyword || undefined,
    page: 1,
    pageSize: 20,
  });
  const items = paged.items ?? [];
  return items.map((c: CategoryEntity) => ({ label: c.categoryName ?? `#${c.id}`, value: c.id }));
}

async function fetchSupplierOptions(keyword: string): Promise<DropDownWithFilterOption[]> {
  const paged = await supplierApiService.getPaginated({
    search: keyword || undefined,
    page: 1,
    pageSize: 20,
  });
  const items = paged.items ?? [];
  return items.map((s: SupplierEntity) => ({ label: s.name ?? `#${s.id}`, value: s.id }));
}

export function ProductFormWithImage({
  mode,
  onSubmit,
  onCancel,
  loading,
  errorMessage,
  onClearError,
  initialValues,
}: ProductFormWithImageProps) {
  const [form] = Form.useForm();
  const [uploading, setUploading] = useState(false);
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [isImageRemoved, setIsImageRemoved] = useState(false);
  const [initialImageUrl, setInitialImageUrl] = useState<string | undefined>(undefined);
  const [initialImageFileId, setInitialImageFileId] = useState<string | undefined>(undefined);

  // Khởi tạo state ảnh khi nhận initialValues (mở form create/update)
  useEffect(() => {
    if (!initialValues) {
      setInitialImageUrl(undefined);
      setInitialImageFileId(undefined);
      setPreviewImage(null);
      setImageFile(null);
      setIsImageRemoved(false);
      return;
    } else {
      form.setFieldsValue({
        productName: initialValues.productName,
        barcode: initialValues.barcode,
        price: initialValues.price,
        unit: initialValues.unit,
        categoryId: initialValues.categoryId,
        supplierId: initialValues.supplierId,
      });

      const iv = initialValues as any;
      const ivUrl = iv.imageUrl as string | undefined;
      const ivFileId = iv.imageFileId as string | undefined;

      setInitialImageUrl(ivUrl);
      setInitialImageFileId(ivFileId);
      setPreviewImage(ivUrl ?? null);
      setImageFile(null);
      setIsImageRemoved(false);
    }
  }, [initialValues, form]);

  // Xử lý khi user chọn file ảnh: chỉ lưu file + tạo preview, KHÔNG upload ngay
  const handleFileSelect = (file: File) => {
    // Có thể thêm validate type/size ở đây nếu cần
    if (!file.type.startsWith('image/')) {
      message.error('Vui lòng chọn file ảnh hợp lệ');
      return false;
    }

    setImageFile(file);
    setIsImageRemoved(false);
    const objectUrl = URL.createObjectURL(file);
    setPreviewImage(objectUrl);
    return false; // Ngăn Upload tự upload
  };

  const handleRemoveImage = async () => {
    // Nếu đang có ảnh cũ trong DB, xóa trên ImageKit ngay
    if (initialImageFileId && !imageFile) {
      try {
        await deleteImageKitFile(initialImageFileId);
      } catch (err) {
        message.error('Không thể xóa ảnh trên ImageKit. Vui lòng thử lại.');
        return;
      }
    }

    // Đánh dấu là user muốn xóa ảnh, clear preview và file tạm
    setIsImageRemoved(true);
    setImageFile(null);
    setPreviewImage(null);
  };

  const handleSubmit = async () => {
    try {
      // Validate required fields first
      await form.validateFields();
      
      // Lấy các giá trị form cho field text/number/select
      const values = form.getFieldsValue([
        'productName',
        'barcode',
        'price',
        'unit',
        'categoryId',
        'supplierId',
      ]);

      let imageUrlToSend: string | undefined;
      let imageFileIdToSend: string | undefined;

      // Xử lý upload ảnh (nếu user chọn ảnh mới)
      if (imageFile) {
        setUploading(true);
        try {
          const result = await uploadImageToImageKit(imageFile);
          imageUrlToSend = result.url;
          imageFileIdToSend = result.fileId;

          // Nếu có ảnh cũ trong DB và chưa remove trước đó, có thể xóa sau khi upload mới thành công
          if (mode === 'update' && initialImageFileId && !isImageRemoved) {
            try {
              await deleteImageKitFile(initialImageFileId);
            } catch {
              // Không chặn flow nếu xóa ảnh cũ thất bại, chỉ log message
              message.warning('Không thể xóa ảnh cũ trên ImageKit. Vui lòng kiểm tra lại sau.');
            }
          }
        } catch (error) {
          message.error(
            error instanceof Error
              ? `Lỗi upload ảnh: ${error.message}`
              : 'Lỗi upload ảnh'
          );
          return;
        } finally {
          setUploading(false);
        }
      } else if (mode === 'update') {
        // Không có file mới trong update
        if (isImageRemoved) {
          // User đã xóa ảnh → clear trên BE
          imageUrlToSend = '';
          imageFileIdToSend = '';
        } else {
          // Giữ nguyên ảnh cũ: không set field image* trong payload để BE không update
          imageUrlToSend = undefined;
          imageFileIdToSend = undefined;
        }
      } else {
        // create + không có ảnh mới -> không gửi field image
        imageUrlToSend = undefined;
        imageFileIdToSend = undefined;
      }

      // Map values theo mode
      if (mode === 'create') {
        const payload: CreateProductRequest = {
          productName: values.productName,
          barcode: values.barcode,
          price: values.price,
          unit: values.unit,
          categoryId: values.categoryId,
          supplierId: values.supplierId,
          imageUrl: imageUrlToSend,
          imageFileId: imageFileIdToSend,
        };
        await onSubmit(payload);
        // Reset form và state sau khi submit thành công (để tránh giữ giá trị cũ khi mở form mới)
        form.resetFields();
        setPreviewImage(null);
        setImageFile(null);
        setIsImageRemoved(false);
      } else {
        const payload: UpdateProductRequest = {};
        if (values.productName !== undefined && values.productName !== '') {
          payload.productName = values.productName;
        }
        if (values.barcode !== undefined && values.barcode !== '') {
          payload.barcode = values.barcode;
        }
        if (values.price !== undefined && values.price !== null) {
          payload.price = values.price;
        }
        if (values.unit !== undefined && values.unit !== '') {
          payload.unit = values.unit;
        }
        if (values.categoryId !== undefined && values.categoryId !== null) {
          payload.categoryId = values.categoryId;
        }
        if (values.supplierId !== undefined && values.supplierId !== null) {
          payload.supplierId = values.supplierId;
        }

        // Chỉ set field ảnh nếu có thay đổi (upload mới hoặc xóa)
        if (imageUrlToSend !== undefined) {
          payload.imageUrl = imageUrlToSend;
        }
        if (imageFileIdToSend !== undefined) {
          payload.imageFileId = imageFileIdToSend;
        }

        console.log('payload', payload)
        await onSubmit(payload);
        // Reset form và state sau khi submit thành công
        form.resetFields();
        setPreviewImage(null);
        setImageFile(null);
        setIsImageRemoved(false);
      }
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        // Validation errors - Ant Design will display them automatically
        return;
      }
      const msg = error instanceof Error ? error.message : 'Thao tác không thành công';
      message.error(msg);
    }
  };

  const currentPreview = previewImage;

  return (
    <div>
      {errorMessage && (
        <Alert
          style={{ marginBottom: 16 }}
          type="error"
          showIcon
          closable
          message={errorMessage}
          onClose={onClearError}
        />
      )}

      <Form 
        form={form} 
        layout="vertical" 
        onFinish={handleSubmit}
        initialValues={initialValues}
      >
        <Form.Item
          name="productName"
          label="Tên sản phẩm"
          rules={[{ required: true, message: 'Vui lòng nhập tên sản phẩm' }]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="barcode"
          label="Barcode"
          rules={mode === 'create' ? [{ required: true, message: 'Vui lòng nhập barcode' }] : []}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="price"
          label="Giá"
          rules={mode === 'create' ? [{ required: true, message: 'Vui lòng nhập giá' }] : []}
        >
          <InputNumber style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          name="unit"
          label="Đơn vị"
          rules={mode === 'create' ? [{ required: true, message: 'Vui lòng nhập đơn vị' }] : []}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="categoryId"
          label="Danh mục"
          rules={mode === 'create' ? [{ required: true, message: 'Vui lòng chọn danh mục' }] : []}
        >
          <DropDownWithFilter
            placeholder="Chọn danh mục"
            fetchOptions={fetchCategoryOptions}
            queryKeyPrefix="category-select"
            fetchOnEmpty={true}
          />
        </Form.Item>

        <Form.Item
          name="supplierId"
          label="Nhà cung cấp"
          rules={mode === 'create' ? [{ required: true, message: 'Vui lòng chọn nhà cung cấp' }] : []}
        >
          <DropDownWithFilter
            placeholder="Chọn nhà cung cấp"
            fetchOptions={fetchSupplierOptions}
            queryKeyPrefix="supplier-select"
            fetchOnEmpty={true}
          />
        </Form.Item>

        {/* Image Upload Section */}
        <Form.Item label="Hình ảnh sản phẩm">
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <Upload
              beforeUpload={(file) => {
                return handleFileSelect(file);
              }}
              showUploadList={false}
              accept="image/*"
              maxCount={1}
            >
              <Button icon={<UploadOutlined />} loading={uploading} disabled={uploading}>
                {uploading ? 'Đang upload...' : 'Upload Image'}
              </Button>
            </Upload>

            {/* Preview Image */}
            {currentPreview && (
              <div style={{ marginTop: '8px' }}>
                <Image
                  src={currentPreview}
                  alt="Product preview"
                  style={{ maxWidth: '200px', maxHeight: '200px', objectFit: 'contain' }}
                  preview={false}
                />
                <Button
                  type="link"
                  danger
                  onClick={handleRemoveImage}
                  style={{ marginTop: '8px', display: 'block' }}
                >
                  Xóa ảnh
                </Button>
              </div>
            )}
          </div>
        </Form.Item>

        <Form.Item style={{ marginTop: 24, marginBottom: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Button onClick={onCancel}>Hủy</Button>
            <Button type="primary" htmlType="submit" loading={loading}>
              {mode === 'create' ? 'Tạo' : 'Cập nhật'}
            </Button>
          </div>
        </Form.Item>
      </Form>
    </div>
  );
}

