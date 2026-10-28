namespace Staybnb.Models;

public enum BookingStatus { Pending, Approved, Rejected, Cancelled, CheckedIn, Completed }
public enum ApplicationStatus { Pending, Approved, Rejected }
public enum PaymentStatus { Paid, Refunded }
public enum CheckInStatus { NotStarted, RulesAccepted, DocumentSubmitted, Verified }
public enum DocumentStatus { Pending, Approved, Rejected }
public enum NotificationType { Info, Success, Warning, Booking, Message, Application, CheckIn }
public enum ActivityType { Login, Logout, Registration, RoleChange, Application, Booking, Payment, Property, CheckIn, Message }
