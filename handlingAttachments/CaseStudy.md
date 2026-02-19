Attachment Synchronization from Dynamics 365 to External Systems

Implemented a specialized integration enhancement to synchronize attachments added to records in:

Microsoft Dynamics 365

to external enterprise systems including:

IBM Maximo

Salesforce

Other REST-based third-party systems

Attachments added in Dynamics needed to be transmitted as comments/notes with file content in downstream systems.

🎯 Business Requirement

When users uploaded documents (images, PDFs, reports) to work orders in Dynamics 365:

The attachment should automatically flow to connected systems

The file must appear as a comment or note

File metadata (filename, type, timestamp, author) should be preserved

Synchronization must be near real-time

Duplicate uploads must be avoided

Manual sharing of attachments was causing datagaps and SLA issues.

Architecture Approach
Integration Pattern: Event-Based Attachment Propagation

Core Components:

Microsoft Dynamics 365

Azure Service Bus

Azure Logic Apps

External REST APIs

Process Flow

1.Attachment added to record in Dynamics.

2.Event triggered via plugin / integration event.

3.Attachment metadata + file content encoded (Base64).

4.Message published to Service Bus Topic.

5.System-specific Logic App subscription processes message.

6.Logic App:

Validates file size

Transforms payload to target schema

Sends as comment/note via API

7.Response logged and status updated

