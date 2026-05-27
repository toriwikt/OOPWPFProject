using System;

namespace OOPWPFProject
{
    public interface IOrderTrackable
    {
        string OrderState { get; set; }
        string AssignedCourier { get; set; }

        void UpdateStatus(string newState);
        bool IsReadyForPickup();
    }

    public abstract class OrderBase
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double Total => Quantity * Price;
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        protected OrderBase() { }

        protected OrderBase(string productName, int quantity, double price)
        {
            ProductName = productName;
            Quantity = quantity;
            Price = price;
        }

        public abstract string GetDetails();

        public static OrderBase operator +(OrderBase a, OrderBase b) =>
            new CombinedOrder(a.ProductName + " + " + b.ProductName,
                              a.Quantity + b.Quantity,
                              a.Price + b.Price);

        public static OrderBase operator -(OrderBase a, OrderBase b) =>
            new CombinedOrder(a.ProductName,
                              a.Quantity - b.Quantity < 0 ? 0 : a.Quantity - b.Quantity,
                              a.Price);

        public static bool operator >(OrderBase a, OrderBase b) => a.Price > b.Price;
        public static bool operator <(OrderBase a, OrderBase b) => a.Price < b.Price;

        public static bool operator ==(OrderBase a, OrderBase b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.Price == b.Price && a.Quantity == b.Quantity;
        }

        public static bool operator !=(OrderBase a, OrderBase b) => !(a == b);

        public override bool Equals(object obj) => obj is OrderBase other && this == other;
        public override int GetHashCode() => Price.GetHashCode() ^ Quantity.GetHashCode();
    }

    internal class CombinedOrder : OrderBase
    {
        public CombinedOrder(string productName, int quantity, double price)
            : base(productName, quantity, price) { }

        public override string GetDetails() =>
            $"Товар: {ProductName}\n" +
            $"Кількість: {Quantity} шт.\n" +
            $"Ціна: {Price:F2} грн\n" +
            $"Сума: {Total:F2} грн";
    }

    public class OnlineOrder : OrderBase, IOrderTrackable
    {
        public string DeliveryAddress { get; set; }
        public string TrackingNumber { get; set; }
        public string OrderState { get; set; } = "Нове";
        public string AssignedCourier { get; set; } = "";

        public OnlineOrder() { }

        public OnlineOrder(string productName, int quantity, double price,
                           string deliveryAddress, string trackingNumber)
            : base(productName, quantity, price)
        {
            DeliveryAddress = deliveryAddress;
            TrackingNumber = trackingNumber;
        }

        public void UpdateStatus(string newState) => OrderState = newState;
        public bool IsReadyForPickup() => OrderState == "Доставлено";

        public override string GetDetails() =>
            $"Товар: {ProductName}\n" +
            $"Кількість: {Quantity} шт.\n" +
            $"Ціна: {Price:F2} грн\n" +
            $"Сума: {Total:F2} грн\n" +
            $"Тип: Онлайн замовлення\n" +
            $"Адреса доставки: {DeliveryAddress}\n" +
            $"Номер відстеження: {TrackingNumber}\n" +
            $"Статус: {OrderState}\n" +
            $"Кур'єр: {AssignedCourier}\n" +
            $"Дата створення: {CreatedAt}\n" +
            $"Готове до отримання: {(IsReadyForPickup() ? "Так" : "Ні")}";
    }

    public class StoreOrder : OrderBase, IOrderTrackable
    {
        public string StoreLocation { get; set; }
        public string PickupTime { get; set; }
        public string OrderState { get; set; } = "Нове";
        public string AssignedCourier { get; set; } = "";

        public StoreOrder() { }

        public StoreOrder(string productName, int quantity, double price,
                          string storeLocation, string pickupTime)
            : base(productName, quantity, price)
        {
            StoreLocation = storeLocation;
            PickupTime = pickupTime;
        }

        public void UpdateStatus(string newState) => OrderState = newState;
        public bool IsReadyForPickup() => OrderState == "Готове до видачі";

        public override string GetDetails() =>
            $"Товар: {ProductName}\n" +
            $"Кількість: {Quantity} шт.\n" +
            $"Ціна: {Price:F2} грн\n" +
            $"Сума: {Total:F2} грн\n" +
            $"Тип: Замовлення в магазині\n" +
            $"Місце: {StoreLocation}\n" +
            $"Час отримання: {PickupTime}\n" +
            $"Статус: {OrderState}\n" +
            $"Кур'єр: {AssignedCourier}\n" +
            $"Дата створення: {CreatedAt}\n" +
            $"Готове до отримання: {(IsReadyForPickup() ? "Так" : "Ні")}";
    }
}