using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OOPWPFProject
{
    public class OrderDto
    {
        public string Type { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public string DeliveryAddress { get; set; }
        public string TrackingNumber { get; set; }
        public string StoreLocation { get; set; }
        public string PickupTime { get; set; }
        public string OrderState { get; set; }
        public string AssignedCourier { get; set; }
        public string CreatedAt { get; set; }

        public static OrderDto FromOrder(OrderBase order)
        {
            var dto = new OrderDto
            {
                ProductName = order.ProductName,
                Quantity = order.Quantity,
                Price = order.Price,
                OrderState = order is IOrderTrackable t ? t.OrderState : "Нове",
                AssignedCourier = order is IOrderTrackable t2 ? t2.AssignedCourier : "",
                CreatedAt = order.CreatedAt
            };

            if (order is OnlineOrder online)
            {
                dto.Type = "Online";
                dto.DeliveryAddress = online.DeliveryAddress;
                dto.TrackingNumber = online.TrackingNumber;
            }
            else if (order is StoreOrder store)
            {
                dto.Type = "Store";
                dto.StoreLocation = store.StoreLocation;
                dto.PickupTime = store.PickupTime;
            }

            return dto;
        }

        public OrderBase ToOrder()
        {
            OrderBase order;
            if (Type == "Online")
            {
                order = new OnlineOrder(ProductName, Quantity, Price,
                    DeliveryAddress, TrackingNumber)
                {
                    OrderState = OrderState ?? "Нове",
                    AssignedCourier = AssignedCourier ?? "",
                    CreatedAt = CreatedAt ?? DateTime.Now.ToString("yyyy-MM-dd")
                };
            }
            else
            {
                order = new StoreOrder(ProductName, Quantity, Price,
                    StoreLocation, PickupTime)
                {
                    OrderState = OrderState ?? "Нове",
                    AssignedCourier = AssignedCourier ?? "",
                    CreatedAt = CreatedAt ?? DateTime.Now.ToString("yyyy-MM-dd")
                };
            }
            return order;
        }
    }

    public static class OrderRepository
    {
        public static List<OrderBase> LoadAll()
        {
            var orders = new List<OrderBase>();
            using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
            {
                conn.Open();
                using (var reader = new SqliteCommand("SELECT * FROM Orders;", conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string type = reader["Type"].ToString();
                        string name = reader["ProductName"].ToString();
                        int qty = int.Parse(reader["Quantity"].ToString());
                        double price = double.Parse(reader["Price"].ToString(), CultureInfo.InvariantCulture);
                        string state = reader["OrderState"].ToString();
                        string courier = reader["AssignedCourier"].ToString();
                        string created = reader["CreatedAt"]?.ToString()
                                         ?? DateTime.Now.ToString("yyyy-MM-dd");

                        OrderBase order;
                        if (type == "Online")
                        {
                            order = new OnlineOrder(name, qty, price,
                                reader["DeliveryAddress"].ToString(),
                                reader["TrackingNumber"].ToString())
                            {
                                OrderState = state,
                                AssignedCourier = courier,
                                CreatedAt = created
                            };
                        }
                        else
                        {
                            order = new StoreOrder(name, qty, price,
                                reader["StoreLocation"].ToString(),
                                reader["PickupTime"].ToString())
                            {
                                OrderState = state,
                                AssignedCourier = courier,
                                CreatedAt = created
                            };
                        }
                        orders.Add(order);
                    }
                }
            }
            return orders;
        }

        public static void Save(OrderBase order)
        {
            using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand(@"
                    INSERT INTO Orders
                        (Type, ProductName, Quantity, Price,
                         DeliveryAddress, TrackingNumber,
                         StoreLocation, PickupTime,
                         OrderState, AssignedCourier, CreatedAt)
                    VALUES
                        (@type, @name, @qty, @price,
                         @addr, @track,
                         @loc, @pickup,
                         @state, @courier, @created);", conn);

                if (order is OnlineOrder o)
                {
                    cmd.Parameters.AddWithValue("@type", "Online");
                    cmd.Parameters.AddWithValue("@addr", o.DeliveryAddress ?? "");
                    cmd.Parameters.AddWithValue("@track", o.TrackingNumber ?? "");
                    cmd.Parameters.AddWithValue("@loc", "");
                    cmd.Parameters.AddWithValue("@pickup", "");
                }
                else if (order is StoreOrder s)
                {
                    cmd.Parameters.AddWithValue("@type", "Store");
                    cmd.Parameters.AddWithValue("@addr", "");
                    cmd.Parameters.AddWithValue("@track", "");
                    cmd.Parameters.AddWithValue("@loc", s.StoreLocation ?? "");
                    cmd.Parameters.AddWithValue("@pickup", s.PickupTime ?? "");
                }

                cmd.Parameters.AddWithValue("@name", order.ProductName);
                cmd.Parameters.AddWithValue("@qty", order.Quantity);
                cmd.Parameters.AddWithValue("@price", order.Price);
                cmd.Parameters.AddWithValue("@state", order is IOrderTrackable t ? t.OrderState : "Нове");
                cmd.Parameters.AddWithValue("@courier", order is IOrderTrackable t2 ? t2.AssignedCourier : "");
                cmd.Parameters.AddWithValue("@created", order.CreatedAt ?? DateTime.Now.ToString("yyyy-MM-dd"));
                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteAll()
        {
            using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
            {
                conn.Open();
                new SqliteCommand("DELETE FROM Orders;", conn).ExecuteNonQuery();
            }
        }

        public static void SaveAll(EntityManager<OrderBase> manager)
        {
            DeleteAll();
            for (int i = 0; i < manager.Count; i++)
                Save(manager[i]);
        }
    }
}