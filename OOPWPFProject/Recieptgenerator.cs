// Підключіть пакет: dotnet add package QuestPDF
// або через NuGet: QuestPDF (версія 2024.x)
//
// У App.xaml.cs або App.xaml додайте у конструктор App():
//   QuestPDF.Infrastructure.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Xml.Linq;

namespace OOPWPFProject
{
    public static class ReceiptGenerator
    {
        /// <summary>
        /// Генерує PDF-чек для замовлення і зберігає його у вказаний шлях.
        /// Повертає шлях до файлу.
        /// </summary>
        public static string Generate(OrderBase order, string outputPath = null)
        {
            if (outputPath == null)
            {
                string dir = Path.Combine("Data", "Receipts");
                Directory.CreateDirectory(dir);
                string safeName = string.Concat(order.ProductName.Split(Path.GetInvalidFileNameChars()));
                outputPath = Path.Combine(dir,
                    $"receipt_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            string orderType = order is OnlineOrder ? "Онлайн замовлення" : "Замовлення в магазині";
            string orderState = order is IOrderTrackable t ? t.OrderState : "—";
            string courier = order is IOrderTrackable t2 ? t2.AssignedCourier : "—";
            string extraLine1 = "";
            string extraLine2 = "";

            if (order is OnlineOrder o)
            {
                extraLine1 = $"Адреса доставки: {o.DeliveryAddress}";
                extraLine2 = $"Номер відстеження: {o.TrackingNumber}";
            }
            else if (order is StoreOrder s)
            {
                extraLine1 = $"Місце отримання: {s.StoreLocation}";
                extraLine2 = $"Час отримання: {s.PickupTime}";
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("ЧЕК ЗАМОВЛЕННЯ")
                            .Bold().FontSize(18);
                        col.Item().AlignCenter().Text("Інформаційна система обліку замовлень")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingTop(12).Column(col =>
                    {
                        // Тип і дата
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(orderType).SemiBold();
                            row.RelativeItem().AlignRight()
                               .Text($"Дата: {order.CreatedAt}").FontColor(Colors.Grey.Darken2);
                        });

                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3); // Поле
                                cols.RelativeColumn(4); // Значення
                            });

                            void AddRow(string label, string value, bool highlight = false)
                            {
                                table.Cell().Padding(4)
                                     .Background(highlight ? Colors.Yellow.Lighten4 : Colors.White)
                                     .Text(label).SemiBold();
                                table.Cell().Padding(4)
                                     .Background(highlight ? Colors.Yellow.Lighten4 : Colors.White)
                                     .Text(value);
                            }

                            AddRow("Товар", order.ProductName);
                            AddRow("Кількість", $"{order.Quantity} шт.");
                            AddRow("Ціна за од.", $"{order.Price:F2} грн");
                            AddRow("Разом", $"{order.Total:F2} грн", highlight: true);
                            AddRow("Статус", orderState);
                            AddRow("Кур'єр", string.IsNullOrWhiteSpace(courier) ? "—" : courier);

                            if (!string.IsNullOrWhiteSpace(extraLine1))
                            {
                                int idx1 = extraLine1.IndexOf(':');
                                AddRow(idx1 > 0 ? extraLine1.Substring(0, idx1) : extraLine1,
                                       idx1 > 0 ? extraLine1.Substring(idx1 + 2) : "");
                            }
                            if (!string.IsNullOrWhiteSpace(extraLine2))
                            {
                                int idx2 = extraLine2.IndexOf(':');
                                AddRow(idx2 > 0 ? extraLine2.Substring(0, idx2) : extraLine2,
                                       idx2 > 0 ? extraLine2.Substring(idx2 + 2) : "");
                            }
                        });

                        col.Item().PaddingTop(16).AlignCenter()
                           .Text($"Дякуємо за замовлення!")
                           .Italic().FontColor(Colors.Grey.Darken2);
                    });

                    page.Footer().AlignCenter()
                        .Text($"Сформовано: {DateTime.Now:dd.MM.yyyy HH:mm}  |  Оператор: {Logger.CurrentUser}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf(outputPath);

            return outputPath;
        }
    }
}