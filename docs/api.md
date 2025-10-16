# ClothingShop.Api - REST API Reference

Base URL: `/api`

## Frontend changes and quick guide

- Payments now support only VNPAY and Stripe. PayOS was removed.
- Create order: POST `/api/orders` with `paymentMethod` = `vnpay | stripe | simulate`.
  - Response includes `payment.url` (open in browser) for `vnpay` and `stripe`.
- After redirect back to FE, fetch GET `/api/orders/{id}` and branch UI by `status`:
  - `paid` → show success; otherwise show failure/pending and allow retry.
- Webhooks for reliability (no FE action):
  - Stripe → `/api/stripe/webhook`
  - VNPAY IPN → `/api/vnpay/ipn`

## Authentication

- POST `/api/auth/register`
  - Body: `{ "email": string, "password": string }`
  - Responses: `200 OK` `{ ok: true }`, `400` when email exists

- POST `/api/auth/login`
  - Body: `{ "email": string, "password": string }`
  - Responses: `200 OK` `{ token: string }`, `401` invalid credentials
  - Usage: Include `Authorization: Bearer <token>` in protected requests

Logout: Client-side only (remove stored JWT)

## Products

- GET `/api/products`
  - Query: `page?: number`, `limit?: number`, `q?: string`
  - Response: `{ data: Product[], total: number, page: number, pages: number }`

- GET `/api/products/{id}`
  - Response: `200 OK Product`, `404` not found

- POST `/api/products` (auth required)
  - Body: `{ name: string, description: string, price: number, image?: string }`
  - Response: `201 Created` Product (Location header)

- PUT `/api/products/{id}` (auth required)
  - Body: `{ name?: string, description?: string, price?: number, image?: string }`
  - Response: `200 OK` Product, `404` not found

- DELETE `/api/products/{id}` (auth required)
  - Response: `200 OK { ok: true }`, `404` not found

`Product` shape:
```
{
  id: string,
  name: string,
  description: string,
  price: number,
  image?: string,
  createdAt: string,
  updatedAt: string
}
```

## Cart (auth required)

Header: `Authorization: Bearer <token>`

- GET `/api/cart`
  - Response: `{ items: Array<{ id: string, product: Product, quantity: number, lineTotal: number }>, total: number }`

- POST `/api/cart`
  - Body: `{ productId: string, quantity: number }`
  - Response: `200 OK` CartItem

- PUT `/api/cart/{id}`
  - Body: `{ quantity: number }`
  - Response: `200 OK` CartItem

- DELETE `/api/cart/{id}`
  - Response: `200 OK { ok: true }`

`CartItem` shape:
```
{
  id: string,
  userId: string,
  productId: string,
  quantity: number,
  createdAt: string
}
```

## Orders (auth required)

- GET `/api/orders`
  - Response: Array of orders with embedded product info per item:
  - Example response item:
```
{
  id: string,
  userId: string,
  totalAmount: number,
  status: "pending" | "paid" | "failed",
  createdAt: string,
  items: [
    {
      id: string,
      orderId: string,
      productId: string,
      quantity: number,
      unitPrice: number,
      product: { id: string, name: string, image?: string, price: number }
    }
  ]
}
```

- GET `/api/orders/{id}`
  - Response: Single order with the same embedded `product` object for each item (see shape above)

- POST `/api/orders`
  - Body:
    - simulate: `{ }` or `{ "paymentMethod": "simulate" }` → creates order and returns it
    - VNPAY: `{ "paymentMethod": "vnpay" }` → returns `{ id, order, payment: { url, debug_sign_data } }`
    - Stripe: `{ "paymentMethod": "stripe" }` → returns `{ id, order, payment: { url } }`
  - Responses: `201 Created`

- POST `/api/orders/{id}/pay` (simulate)
  - Response: `200 OK` Order with `status: "paid"`

- DELETE `/api/orders/{id}`
  - Response: `200 OK { ok: true }`

Order shapes:
```
Order {
  id: string,
  userId: string,
  totalAmount: number,
  status: "pending" | "paid" | "cancelled",
  createdAt: string,
  items: OrderItem[]
}

OrderItem {
  id: string,
  orderId: string,
  productId: string,
  quantity: number,
  unitPrice: number
}
```

## Payments

### VNPAY
- POST `/api/vnpay/create/{orderId}` → trả `url` thanh toán cho đơn hàng
- GET `/api/vnpay/return` → xác thực chữ ký và cập nhật order
- GET `/api/vnpay/ipn` → IPN do VNPAY gọi, xác thực và cập nhật order, trả `200 OK`

Ghi chú quan trọng (theo tài liệu VNPAY):
- `vnp_Amount` là VND đơn vị nhỏ nhất (×100). Hệ thống quy đổi USD→VND theo tỷ giá sống `exchangerate.host` (fallback cấu hình `VnPay:UsdToVndRate`).
- `vnp_CreateDate` theo múi giờ GMT+7 định dạng `yyyyMMddHHmmss`.
- Ký HMAC-SHA512 trên chuỗi query đã form-encode (space = '+') với khóa `VnPay:HashSecret`, sắp xếp key theo ASCII (Ordinal). Thêm `vnp_SecureHashType=HmacSHA512` vào URL.
- Xác thực Return/IPN bằng cách lấy raw query trước khi decode, bỏ `vnp_SecureHash`/`vnp_SecureHashType`, ký lại chuỗi encode để so khớp.
- Thành công khi: `vnp_ResponseCode == "00"` và `vnp_TransactionStatus == "00"` → cập nhật `status = "paid"`, ngược lại `failed`.
  - Tài liệu tham khảo: [VNPAY Pay docs](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html)

### Stripe
- Checkout Session is created server-side; client opens `payment.url`.
- Webhook: POST `/api/stripe/webhook` (Stripe → Backend)
  - Verify using `Stripe:WebhookSecret` header `Stripe-Signature`.
  - On `checkout.session.completed`, order is updated to `paid`.

## Error Handling

- Format: RFC7807 ProblemDetails (`application/problem+json`)
- Unauthorized: `401`
- Not found: `404`
- Validation errors: `400` with `errors` extension:
```
{
  type: string,
  title: string,
  status: 400,
  traceId: string,
  correlationId?: string,
  errors: {
    fieldName: ["message1", "message2"]
  }
}
```
- 500 Internal error: generic title; in Development includes `detail` & `stackTrace`.

Header hỗ trợ theo dõi: gửi/nhận `X-Correlation-Id` để trace request end-to-end.

## Example Fetch Usage

```ts
// Login
const res = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});
const { token } = await res.json();

// Authorized request
const products = await fetch('/api/products', {
  headers: { Authorization: `Bearer ${token}` }
});
```
