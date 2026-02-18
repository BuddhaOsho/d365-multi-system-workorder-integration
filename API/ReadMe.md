# 🧩 Service Request API (OpenAPI 3.0)

Enterprise REST API to **create and manage service requests** (commonly used in Facility/IFM workflows).  
This single file includes documentation, sample requests, Postman artifacts, and the **full OpenAPI 3.0 specification** for easy import into Swagger Editor.

---

## 🔗 Base URL

> All endpoints below are relative to this base path.

---

## 📘 View the API in Swagger

1. Copy the **OpenAPI (YAML)** from the end of this file.
2. Open https://editor.swagger.io/
3. Paste the YAML to get interactive docs.

---

## 🚀 Endpoints Overview

### 1) Create a Service Request  
**POST** `/servicerequest`  
Creates a new service request (ticket).

- **Request Body** → `ServiceRequest`
  - Required: `externalIdentifier`, `externalServiceCategory`, `reportedBy`, `affectedPerson`, `siteCode`, `locationCode`, `priority`, `requestType`, `title`
- **200 Response** → `ServiceRequestResponse` (returns `identifier`, `externalIdentifier`)

---

### 2) List Requests for a User  
**GET** `/servicerequest/list?userid={email}&lastdays={n}`  
Returns a `ServiceRequestList`.

- Required: `userid` (email)
- Optional: `lastdays` (include cancelled/resolved tickets within last N days)

---

### 3) Sites for a Contract  
**GET** `/servicerequest/site?contract={id}`  
Returns `SiteResponse` (array of `{ code, description }`).

---

### 4) Locations for a Site/Contract  
**GET** `/servicerequest/location?contract={id}&site={code}`  
Returns `LocationResponse` (hierarchical locations with `level`, `parent`, `description`, `type`).

---

### 5) External Service Catalogue  
**GET** `/servicerequest/externalservicecatalogue`  
Returns `ExternalServiceCatalogueResponse` (vendor/external categories).

---

### 6) Service Catalogue (by Site)  
**GET** `/servicerequest/servicecatalogue?contract={id}&site={code}`  
Returns `ServiceCatalogueResponse` (internal categories/services).

---

## 🧠 Core Data Models (Schemas)

- **ServiceRequest**: main ticket object (contacts, site/location, priority, category, notes, SLA timestamps)
- **Contact**: person details (+ optional `logs`)
- **Site / Location**: facility structure; `Location` supports hierarchy via `level` + `parent`
- **ServiceCategory / ExternalServiceCategory**: internal vs vendor catalogs
- **Note / Attachment**: comments with optional Base64 file uploads
- **ErrorResponse**: standard error payload with `Message`, `ExceptionMessage`, `ExceptionType`, `Detail`

> Full schema details are in the OpenAPI spec at the bottom of this file.

---

## 🏗 Architecture (Mermaid)

```mermaid
flowchart LR
A[Client App / D365 / Power App] --> B[Logic App / Integration Layer]
B -->|HTTP JSON| C[Service Request API]
C --> D[IFM Backend / Ticketing]



