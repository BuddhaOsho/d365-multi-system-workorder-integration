# d365-multi-system-workorder-integration

Dynamics 365 Multi-System Work Order Integration
📌 Overview
 
This repository demonstrates an enterprise event-driven integration architecture for synchronizing Work Orders between Dynamics 365 and multiple external systems including IBM Maximo, Salesforce, and third-party vendor systems.
 
The architecture supports:
 
Work Order creation synchronization
 
Status update synchronization
 
Attachment transfer as Notes
 
Multi-system routing using correlation filters
 
Reliable message processing
 
Architecture Flow
Step 1: Work Order Created in Dynamics 365
 
A C# Plugin triggers on Work Order Create event.
 
The plugin constructs a JSON payload.
 
Payload is published to Azure Service Bus Topic.
 
Step 2: Message Routing
 
Service Bus Topic uses Correlation Filters.
 
Messages are routed to dedicated Subscriptions:
 
Maximo Subscription
 
Salesforce Subscription
 
Third-Party Vendor Subscription
 
Step 3: Azure Logic Apps Processing
 
Logic Apps listen to each subscription.
 
Retrieve and validate additional details via APIs.
 
Transform payload as per target system schema.
 
Call external APIs to:
 
Create Work Orders
 
Update Status
 
Sync Attachments (as Notes)
 
Step 4: Monitoring & Reliability
 
Retry policies enabled.
 
Dead-letter queue for failed messages.
 
Centralized logging and monitoring.
 
🔐 Security Implementation
 
Token-based API authentication (OAuth 2.0)
 
Secure API endpoints
 
Managed identity usage where applicable
 
Encrypted payload transmission
 
🔁 Attachment Synchronization
 
Attachments from Dynamics 365 are extracted.
 
Converted to Base64 format.
 
Transmitted via API.
 
Created as Notes in target systems.
 
🧰 Technologies Used
 
Microsoft Dynamics 365
 
Azure Service Bus (Topics & Subscriptions)
 
Azure Logic Apps
 
REST APIs
 
JSON Transformation
 
Correlation Filters
 
OAuth 2.0 Authentication

Details:
/architecture        → High-level architecture explanation
/plugin              → Sample Dynamics 365 plugin implementation
/sample-payload      → Example JSON message structure
/logicapp-definition → Logic App processing overview
 
This repository demonstrates an event-driven enterprise integration pattern using Azure Service Bus Topics and Logic Apps for multi-system Work Order synchronization.
 
