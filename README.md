# Supermarket WhatsApp Bot — .NET Core Starter

Built directly on Meta's WhatsApp Cloud API (no BSP), using ASP.NET Core 8 +
EF Core (Npgsql) against your existing Postgres schema.

## Project layout

```
SupermarketBot/
├── Controllers/
│   └── WebhookController.cs    # webhook verification + incoming message handling
├── Flows/
│   ├── GreetingFlow.cs         # main menu
│   └── BrowsingFlow.cs         # category/product listing
├── Services/
│   ├── WhatsAppService.cs      # calls Meta's Graph API to send messages
│   ├── CustomerService.cs
│   ├── ConversationService.cs  # bot session state machine
│   └── CatalogService.cs
├── Models/                     # EF Core entities mapped to your existing tables
├── Data/
│   └── AppDbContext.cs
├── Program.cs                  # app startup / DI wiring
└── appsettings.json            # config (DB connection, WhatsApp credentials)
```

## Setup

1. **Install the .NET 8 SDK** (if not already installed):
   https://dotnet.microsoft.com/download

2. **Restore packages:**
   ```
   dotnet restore
   ```

3. **Configure secrets.** Don't put real credentials in `appsettings.json` if
   this repo will be pushed anywhere. Instead use `dotnet user-secrets` locally:
   ```
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=supermarket_bot;Username=youruser;Password=yourpass"
   dotnet user-secrets set "WhatsApp:PhoneNumberId" "your_phone_number_id"
   dotnet user-secrets set "WhatsApp:AccessToken" "your_access_token"
   dotnet user-secrets set "WhatsApp:VerifyToken" "choose_a_random_secret_string"
   ```
   In production, use environment variables or Azure Key Vault instead.

4. **Run it:**
   ```
   dotnet run
   ```
   Default: `http://localhost:5000` (check console output for the actual port).

5. **Expose it publicly for Meta's webhook** (local dev):
   ```
   ngrok http 5000
   ```
   Use the ngrok HTTPS URL + `/webhook` as your callback URL in the Meta App
   dashboard (WhatsApp → Configuration → Webhook), with the same
   `WhatsApp:VerifyToken` value you configured above.

6. **Test:** message your WhatsApp test number with "hi" — you should get
   the main menu back, and see rows appear in your `customers`,
   `conversations`, and `messages` tables.

## What's implemented

- Webhook verification (`GET /webhook`)
- Incoming message handling (`POST /webhook`) — text and interactive
  button/list replies
- Customer auto-creation + loyalty account creation
- Conversation/session state tracked in Postgres (`conversations` table)
- Message logging (`messages` table)
- Greeting → main menu flow
- Category browsing → product listing flow

## What's next (not yet built — extend `Flows/` and `Controllers/WebhookController.cs`)

- Add-to-cart (`carts` / `cart_items`)
- Checkout: delivery/pickup, address, time slot
- Payment (start with cash-on-delivery or a payment link)
- Order creation (`orders` / `order_items`)
- Order tracking flow (`MENU_TRACK`)
- Support ticket flow (`MENU_SUPPORT`)
- Message template registration for promotional/marketing sends
  (required by WhatsApp outside the 24-hour reply window)

## Notes

- `ContextData` on `Conversation` is mapped as a raw jsonb string and
  patched manually in `ConversationService.UpdateStateAsync` — this keeps
  the state machine flexible without needing schema migrations as you add
  new flows.
- The webhook POST handler awaits processing synchronously rather than
  fire-and-forget, since EF Core's `DbContext` is request-scoped and would
  be disposed before a detached background task finished. At higher
  volume, move to a proper queue (Hangfire, Azure Service Bus, etc.) and
  return `200 OK` immediately.
