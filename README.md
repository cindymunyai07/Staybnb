# Staybnb

**An Airbnb-style property rental platform built with ASP.NET Core MVC.**

`.NET 8` · `ASP.NET Core MVC` · `EF Core 8` · `SQL Server (LocalDB by default)` · `ASP.NET Core Identity` · `Bootstrap 5`

> Built as a coursework project for Programming 742 at Richfield. Staybnb is a learning project and is not affiliated with Airbnb.

## Overview

Staybnb covers the full marketplace lifecycle. A visitor registers as a **Guest**, browses and books properties, completes an online check-in, and can apply to become a **Host**. **Admins** review host applications and monitor an activity log, and a **SuperAdmin** is the only account that can promote Guests to Admin.

**Highlights**

- Role-based access (Guest, Host, Admin, SuperAdmin) with a transactional Guest-to-Host role swap on approval
- Property gallery with filters, five-photo listings, guest reviews, ratings and a Superhost badge
- Booking engine with a live price, double-booking protection and a full status workflow
- Multi-step online check-in with private ID and passport document verification
- Database-backed messaging and a notification centre (read and unread status)
- Activity logging of key security and business actions
- Property map using Leaflet and OpenStreetMap

## Screenshots

## Screenshots

### Home page
<img width="700" alt="Staybnb home page showing the property gallery" src="https://github.com/user-attachments/assets/a8e44d4e-e5c7-4277-a22b-0d15ec97d8bc" />

*Property gallery with search and filters*

### Property details
<img width="700" alt="What the place offers" src="https://github.com/user-attachments/assets/7629509b-8a5a-4066-99c4-ff7c91b1617e" />

*Property page showing what the place offers*

### Payment
<img width="700" alt="Payment details" src="https://github.com/user-attachments/assets/ec35fe63-279d-4986-8395-9626f2459fd9" />

*Payment details for a booking (the payment is simulated)*

### Host application
<img width="700" alt="Host Application" src="https://github.com/user-attachments/assets/c75c7066-6c1b-4e04-af87-f1af82004143" />

*Guests apply to become hosts with property details and images*

### Notifications
<img width="700" alt="Notification" src="https://github.com/user-attachments/assets/346774ee-ab77-43f6-a53f-da7d54fc7b25" />

*Notification centre with read and unread status*


## 1. Run it

**Requirements:** .NET 8 SDK and SQL Server (LocalDB or full SQL Server). Set the server in `ConnectionStrings:DefaultConnection` in `appsettings.json`.

The app opens on `https://localhost:7244`. Run `dotnet dev-certs https --trust` once if the browser warns about the certificate.

The EF Core migrations are in the `Migrations` folder. If that folder is ever missing, run `migrate.bat` once to generate it before starting the app.

**What is seeded and where**

- **In the migration (model `HasData`):** the four roles (Guest, Host, Admin, SuperAdmin) and the 12 amenities.
- **In code on startup (`Data/RoleSeeder.cs`, `Data/DataSeeder.cs`):** the SuperAdmin and demo accounts (passwords must be hashed at runtime), plus the demo listings, bookings and reviews.

## 2. Seeded accounts

All demo passwords are `Staybnb@123`. These accounts are for local demonstration only.

| Role | Email | Notes |
|---|---|---|
| SuperAdmin | `superadmin@staybnb.com` | Created by `RoleSeeder` on every startup. The only account that can promote Guests to Admin |
| Admin | `admin@staybnb.com` | Reviews host applications, views the activity log |
| Host | `host@staybnb.com` | Owns 4 approved properties, has 1 pending and 1 approved booking |
| Host | `host2@staybnb.com` | Second host who owns about half of the extra listings |
| Guest | `guest@staybnb.com` | Has the pending and approved bookings above |
| Guest | `guest2@staybnb.com` | Has a pending host application ("Seaside Studio") for the Admin to review |

The catalogue has **36 listings**, each with a five-photo gallery and seeded guest reviews (so ratings and the Superhost badge work). They cover **10 cities** (Cape Town, Johannesburg, Durban, Umhlanga, Pretoria, Stellenbosch, Gqeberha, Knysna, Hermanus, Drakensberg) and all **7 property types**. The seeders in `Data/DataSeeder.cs` top up an existing database on startup, so no reset is needed.

Listing photos are free-to-use Unsplash images loaded from their CDN, so the first page load needs internet (the bundled drawings in `wwwroot/images/seed` are the fallback if one fails). The property map uses Leaflet with OpenStreetMap tiles and Nominatim geocoding, and needs no API key.

## 3. 10-minute test script

1. Register a new user. They are automatically a Guest (check **Admin > Activity Logs**).
2. Become a Host: try submitting without an image (blocked), then with an image.
3. Log in as `admin@staybnb.com`, open **Host Requests**, review and **Approve**. The applicant now has the Host role and no longer has Guest (log in as them: the Host dashboard appears and guest pages are denied).
4. Log in as `superadmin@staybnb.com`, open **Users** and promote a Guest to Admin (Admins cannot see this page).
5. As `guest@staybnb.com`: browse and filter, open a property, pick dates (live price), pay (simulated; the amount must equal the total), and see it under **My Trips**.
6. As `host@staybnb.com`: **Booking Requests > Approve**. (**Host > My Properties > Check-in process** edits rules and documents; **Deactivate** hides a listing.)
7. As the guest: **My Trips > Online check-in**, accept the rules and upload an ID or passport (JPG, PNG or PDF).
8. As the host: **Guest Documents > Approve**, and the booking becomes `CheckedIn`. (Reject lets the guest re-upload.)
9. Try **Messages** (Guest and Host, Admin and anyone) and the bell **Notifications** (Unread to Read is stored in the database).

## 4. Where things are

| Feature | Location |
|---|---|
| Roles, SuperAdmin seeding | `Data/RoleSeeder.cs`, `Program.cs` |
| Guest to Host swap (transactional) | `AdminController.ApproveApplication` |
| Role-based access | `[Authorize(Roles = ...)]` on every controller and action |
| Host application and mandatory image | `HostApplicationController.Create` |
| Pricing: (Price x Nights) + Cleaning + Service (12%) | `Helpers/AppHelpers.cs` (`PriceCalculator`) |
| Double-booking and guest-count checks, status workflow, refunds | `Services/BookingService.cs` |
| Multi-step check-in and document verification | `BookingsController`, `HostController.VerifyDocument` |
| Private ID documents (not in `wwwroot`) | `Services/FileStorage.cs`, `FilesController` |
| Messaging and notifications | `MessagesController`, `NotificationsController`, bell ViewComponent |
| Activity logging | `Services/ActivityLogger.cs`, called from every key action |

## 5. Design decisions

- Hosts cannot book (register a second Guest account). `HostApplication/Create` is shared by Guests (first application) and Hosts (additional property).
- Payment is simulated (no bank checks), but the amount must equal the server-calculated total. Rejected and cancelled bookings are marked Refunded.
- Booking status flow: `Pending -> Approved -> CheckedIn -> Completed`. Rejected and Cancelled bookings free the dates.
- The overlap check runs inside a Serializable transaction to stop simultaneous double bookings.
- All foreign keys use `Restrict` (SQL Server multiple-cascade-path rule), and nothing hard-deletes parents.

## 6. Security notes

- Role checks use `[Authorize(Roles = ...)]`. Hosts are additionally scoped by `HostId` and guests by `GuestId` on every action.
- Anti-forgery tokens are required on every POST (global filter).
- Accounts lock for 5 minutes after 5 wrong passwords. Failed logins and lockouts are written to the activity log.
- Uploads are limited to 5 MB and checked by extension and by file signature. ID documents are stored outside `wwwroot` and served only to the guest, the property's host or staff.
- The payment is simulated: card details are format-checked only and are never stored (only the last four digits).
- The seeded demo accounts use a shared, published password. Change the passwords and remove the startup seeding before any real deployment.

## 7. Known limitations

- No automated tests; verification is the manual script in section 3.
- The service fee rate (12%) and the check-in and check-out times shown on a listing are fixed in code, not configurable per property.
- The map and photos are loaded from OpenStreetMap and Unsplash, so those need internet access.
