namespace PayBeancoins.Models;

public record OrderItem(string Name, int Quantity);

// order.created payload, as published by OrderBeans
public record OrderCreatedPayload(List<OrderItem> Items, decimal TotalAmount, string Email);

public record PaymentSucceededPayload(decimal AmountCharged, List<OrderItem> Items);

public record PaymentFailedPayload(string Reason);
