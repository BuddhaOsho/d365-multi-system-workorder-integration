Event-Driven Integration Pattern:

This solution follows a publish-subscribe (Pub/Sub) architecture using Azure Service Bus Topics.
Flow:
Work Order created in Dynamics 365
C# Plugin publishes JSON payload to Service Bus Topic
Correlation Filters route message to specific subscriptions:
Maximo
Salesforce
Third-Party Vendors

Azure Logic Apps listen to each subscription
Logic Apps validate and transform payload
 
Target system APIs are called
 
Retry & dead-letter handling ensures reliability
 
