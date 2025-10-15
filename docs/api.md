# ClothingShop.Api - REST API Reference

Base URL: `/api`

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
  - Response: `Order[]`

- GET `/api/orders/{id}`
  - Response: `Order`

- POST `/api/orders`
  - Body:
    - simulate: `{ }` or `{ "paymentMethod": "simulate" }` → creates order and returns it
    - PayOS: `{ "paymentMethod": "payos" }` → returns `{ order, payos }` with PayOS payload (e.g., payment link)
    - VNPAY: `{ "paymentMethod": "vnpay" }` → returns `{ order, vnpay: { url } }` payment URL
  - Responses: `201 Created`

- POST `/api/orders/{id}/pay` (simulate)
  - Response: `200 OK` Order with `status: "paid"`

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

## PayOS Webhook

## VNPAY
- POST `/api/vnpay/create/{orderId}` → trả `url` thanh toán cho đơn hàng
- GET `/api/vnpay/return` → endpoint return/callback; hệ thống xác thực chữ ký và cập nhật order `status = "paid"` khi `vnp_TransactionStatus == "00"`

- POST `/api/payos/webhook`
  - Used by PayOS to confirm payment
  - Verifies signature with `PayOS:WebhookSecret`
  - On success: updates corresponding order `status` to `"paid"`

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
