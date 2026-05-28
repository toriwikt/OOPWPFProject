using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace OOPWPFProject
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<OrderViewModel> Orders { get; set; }
        private EntityManager<OrderBase> _manager;
        private string _currentRole;
        private bool _isLoggingOut = false;
        private bool _isEditMode = false;
        private int _editIndex = -1;

        private static readonly string DataDir = "Data";
        private static readonly string DeliveredFile = Path.Combine(DataDir, "delivered.json");

        private string _searchText = "";
        private string _filterStatus = "Всі";
        private string _filterType = "Всі";

        public MainWindow(string role)
        {
            Orders = new ObservableCollection<OrderViewModel>();
            _manager = new EntityManager<OrderBase>();
            InitializeComponent();

            _currentRole = role;
            DataContext = this;

            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            CurrentUserBlock.Text = Logger.CurrentUser;
            CurrentRoleBlock.Text = role == "Admin" ? "Адміністратор" : "Оператор";
            CurrentRoleBlock.Foreground = role == "Admin"
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6A75E"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8A9BBE"));

            LoadOrders();
            textboxCreatedAt.SelectedDate = DateTime.Now;
            ApplyRoleRestrictions();
            UpdateStatusBar();
        }

        // ── Ролі ─────────────────────────────────────────────────
        private void ApplyRoleRestrictions()
        {
            if (_currentRole == "Operator")
            {
                DeleteButton.Visibility = Visibility.Collapsed;
                SaveDeliveredButton.Visibility = Visibility.Collapsed;
                ExportCsvButton.Visibility = Visibility.Collapsed;
                EditModeButton.Visibility = Visibility.Collapsed;
                NavStats.Visibility = Visibility.Collapsed;
                NavOps.Visibility = Visibility.Collapsed;
                SortComboBox.Visibility = Visibility.Collapsed;
            }
        }

        // ── Вихід ────────────────────────────────────────────────
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _isLoggingOut = true;
            SaveOrders();
            Logger.Log("Вихід", $"Користувач '{Logger.CurrentUser}' вийшов із системи");
            new LoginWindow().Show();
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveOrders();
            base.OnClosing(e);
            if (!_isLoggingOut)
            {
                Logger.Log("Вихід", $"Користувач '{Logger.CurrentUser}' закрив програму");
                Application.Current.Shutdown();
            }
        }

        // ── Навігація ────────────────────────────────────────────
        private void NavOrders_Click(object sender, MouseButtonEventArgs e) => ShowPage("Orders");
        private void NavOps_Click(object sender, MouseButtonEventArgs e) => ShowPage("Ops");
        private void NavStats_Click(object sender, MouseButtonEventArgs e) { ShowPage("Stats"); UpdateStats(); }

        private void ShowPage(string page)
        {
            PageOrders.Visibility = page == "Orders" ? Visibility.Visible : Visibility.Collapsed;
            PageOps.Visibility = page == "Ops" ? Visibility.Visible : Visibility.Collapsed;
            PageStats.Visibility = page == "Stats" ? Visibility.Visible : Visibility.Collapsed;

            SetNavActive(NavOrders, page == "Orders");
            SetNavActive(NavOps, page == "Ops");
            SetNavActive(NavStats, page == "Stats");
        }

        private void SetNavActive(Border border, bool active)
        {
            border.Background = active
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6A75E"))
                : Brushes.Transparent;

            string color = active ? "#1A2235" : "#8A9BBE";
            if (border.Child is StackPanel sp)
                foreach (var child in sp.Children)
                    if (child is TextBlock tb)
                        tb.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(color));
        }

        // ── StatusBar і Toast ─────────────────────────────────────
        private void UpdateStatusBar(string message = "")
        {
            StatusBlock.Text = message;
            TotalCountBlock.Text = Orders.Count.ToString();
            StatusCountBlock.Text = $"Замовлень: {Orders.Count}";
        }

        private async void ShowToast(string message, bool success = true)
        {
            ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                success ? "#2E7653" : "#762E3F"));
            ToastText.Text = message;
            ToastBorder.Visibility = Visibility.Visible;
            await Task.Delay(2500);
            ToastBorder.Visibility = Visibility.Collapsed;
        }

        // ── БД ───────────────────────────────────────────────────
        private void SaveOrders()
        {
            try
            {
                OrderRepository.SaveAll(_manager);
                Logger.Log("Збережено", $"Збережено {_manager.Count} замовлень у БД");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrders()
        {
            try
            {
                foreach (var order in OrderRepository.LoadAll())
                {
                    _manager.Add(order);
                    Orders.Add(new OrderViewModel(order, _manager.Count - 1)); // ось тут додай індекс
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Помилка зчитування: {ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDelivered_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var delivered = new List<OrderDto>();
                for (int i = 0; i < _manager.Count; i++)
                {
                    var order = _manager[i];
                    if (order is IOrderTrackable t &&
                        (t.OrderState == "Доставлено" || t.OrderState == "Готове до видачі"))
                        delivered.Add(OrderDto.FromOrder(order));
                }

                File.WriteAllText(DeliveredFile,
                    JsonConvert.SerializeObject(delivered, Formatting.Indented));
                Logger.Log("Збережено", $"Збережено {delivered.Count} доставлених замовлень");
                ShowToast($"✔ Збережено {delivered.Count} доставлених замовлень");
                UpdateStatusBar($"Збережено {delivered.Count} доставлених");
            }
            catch (System.Exception ex)
            {
                ShowToast($"✘ Помилка: {ex.Message}", false);
            }
        }

        // ── Пошук і фільтрація ───────────────────────────────────
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchBox.Text.Trim().ToLower();
            ApplyFilters();
        }

        private void FilterStatus_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterStatusComboBox.SelectedItem is ComboBoxItem item)
            {
                _filterStatus = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void FilterType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterTypeComboBox.SelectedItem is ComboBoxItem item)
            {
                _filterType = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            FilterStatusComboBox.SelectedIndex = 0;
            FilterTypeComboBox.SelectedIndex = 0;
            _searchText = "";
            _filterStatus = "Всі";
            _filterType = "Всі";
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (Orders != null && _manager != null && _manager.Count > 0)
            {
                var temp = new List<OrderViewModel>();
                for (int i = 0; i < _manager.Count; i++)
                {
                    var order = _manager[i];

                    bool matchSearch = string.IsNullOrEmpty(_searchText) ||
                        order.ProductName.ToLower().Contains(_searchText);

                    bool matchStatus = _filterStatus == "Всі" ||
                        (order is IOrderTrackable t && t.OrderState == _filterStatus);

                    bool matchType = _filterType == "Всі" ||
                        (_filterType == "Онлайн" && order is OnlineOrder) ||
                        (_filterType == "Магазин" && order is StoreOrder);

                    if (matchSearch && matchStatus && matchType)
                        temp.Add(new OrderViewModel(order, i));
                }

                Orders.Clear();
                foreach (var vm in temp)
                    Orders.Add(vm);

                UpdateStatusBar($"Знайдено: {Orders.Count}");
            }
        }

        // ── Статистика ───────────────────────────────────────────
        private void UpdateStats()
        {
            DateTime dateFrom = StatDateFrom.SelectedDate ?? DateTime.MinValue;
            DateTime dateTo = (StatDateTo.SelectedDate ?? DateTime.Now).Date.AddDays(1).AddSeconds(-1);

            int total = 0;
            double sum = 0;
            int online = 0, store = 0, delivered = 0;

            var statusCounts = new Dictionary<string, int>
            {
                { "Нове", 0 }, { "В обробці", 0 }, { "Доставлено", 0 },
                { "Готове до видачі", 0 }, { "Скасовано", 0 }
            };

            for (int i = 0; i < _manager.Count; i++)
            {
                var order = _manager[i];

                if (DateTime.TryParse(order.CreatedAt, out DateTime created))
                {
                    if (created < dateFrom || created > dateTo)
                        continue;
                }

                total++;
                sum += order.Total;

                if (order is OnlineOrder) online++;
                else if (order is StoreOrder) store++;

                if (order is IOrderTrackable t)
                {
                    if (t.OrderState == "Доставлено") delivered++;
                    if (statusCounts.ContainsKey(t.OrderState))
                        statusCounts[t.OrderState]++;
                }
            }

            string periodLabel = $"{dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}";
            if (StatPeriodLabel != null)
                StatPeriodLabel.Text = $"Період: {periodLabel}";

            StatTotalSum.Text = $"{sum:F2} грн";
            StatTotalCount.Text = total.ToString();
            StatOnlineCount.Text = online.ToString();
            StatStoreCount.Text = store.ToString();
            StatDeliveredCount.Text = delivered.ToString();
            StatAvgSum.Text = total > 0 ? $"{sum / total:F2} грн" : "0.00 грн";

            int maxCount = statusCounts.Values.Max() > 0 ? statusCounts.Values.Max() : 1;
            StatStatusList.ItemsSource = statusCounts.Select(kv => new StatusBarItem
            {
                Status = kv.Key,
                Count = kv.Value,
                BarWidth = (double)kv.Value / maxCount * 300
            }).ToList();
        }

        private void StatApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            UpdateStats();
        }

        private void StatResetFilter_Click(object sender, RoutedEventArgs e)
        {
            StatDateFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            StatDateTo.SelectedDate = DateTime.Now;
            UpdateStats();
        }

        // ── Експорт CSV ──────────────────────────────────────────
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "orders_export",
                DefaultExt = ".csv",
                Filter = "CSV файли (.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    const char Sep = ';';

                    // Екранує поле: загортає в лапки, якщо містить роздільник, лапки або перенос рядка
                    string Q(string value)
                    {
                        if (value == null) value = "";
                        if (value.IndexOfAny(new[] { Sep, '"', '\n', '\r' }) >= 0)
                            return "\"" + value.Replace("\"", "\"\"") + "\"";
                        return value;
                    }

                    // Число у форматі Excel поточної локалі (крапка або кома залежно від ОС)
                    string N(double v) => v.ToString("F2", System.Globalization.CultureInfo.CurrentCulture);

                    var sb = new StringBuilder();
                    // sep= підказка для Excel — автоматично обирає роздільник
                    sb.AppendLine($"sep={Sep}");
                    sb.AppendLine(string.Join(Sep.ToString(), new[]
                    {
                        "Індекс", "Назва товару", "Кількість", "Ціна (грн)", "Сума (грн)",
                        "Тип", "Статус", "Кур'єр", "Готове до отримання", "Дата створення",
                        "Адреса / Місце", "Трекінг / Час"
                    }));

                    for (int i = 0; i < _manager.Count; i++)
                    {
                        var order = _manager[i];
                        string type = order is OnlineOrder ? "Онлайн" : "Магазин";
                        string status = order is IOrderTrackable t ? t.OrderState : "-";
                        string courier = order is IOrderTrackable t2 ? t2.AssignedCourier : "-";
                        string ready = order is IOrderTrackable t3
                            ? (t3.IsReadyForPickup() ? "Так" : "Ні") : "-";

                        string extra1 = "", extra2 = "";
                        if (order is OnlineOrder o)
                        {
                            extra1 = o.DeliveryAddress ?? "";
                            extra2 = o.TrackingNumber ?? "";
                        }
                        else if (order is StoreOrder s)
                        {
                            extra1 = s.StoreLocation ?? "";
                            extra2 = s.PickupTime ?? "";
                        }

                        sb.AppendLine(string.Join(Sep.ToString(), new[]
                        {
                            Q(i.ToString()),
                            Q(order.ProductName),
                            Q(order.Quantity.ToString()),
                            Q(N(order.Price)),
                            Q(N(order.Total)),
                            Q(type),
                            Q(status),
                            Q(courier),
                            Q(ready),
                            Q(order.CreatedAt),
                            Q(extra1),
                            Q(extra2)
                        }));
                    }

                    // Windows-1251 — Excel на Windows коректно читає кирилицю без налаштувань
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.GetEncoding(1251));
                    Logger.Log("Експорт", $"Експортовано {_manager.Count} замовлень до {dialog.FileName}");
                    ShowToast($"✔ Експортовано {_manager.Count} замовлень");
                    UpdateStatusBar($"Експорт завершено: {dialog.FileName}");
                }
                catch (System.Exception ex)
                {
                    ShowToast($"✘ Помилка експорту: {ex.Message}", false);
                }
            }
        }

        // ── Режим редагування ─────────────────────────────────────
        private void EditMode_Click(object sender, RoutedEventArgs e)
        {
            if (!(OrdersDataGrid.SelectedItem is OrderViewModel selectedVm))
            {
                ShowToast("✘ Спочатку виділіть замовлення в таблиці", false);
            }
            else
            {
                _editIndex = Orders.IndexOf(selectedVm);
                var order = _manager[_editIndex];

                textboxName.Text = order.ProductName;
                textboxQuantity.Text = order.Quantity.ToString();
                textboxPrice.Text = order.Price.ToString();
                textboxCreatedAt.SelectedDate = DateTime.TryParse(order.CreatedAt, out DateTime d)
                    ? d : DateTime.Now;

                if (order is OnlineOrder o)
                {
                    RadioOnline.IsChecked = true;
                    textboxDeliveryAddress.Text = o.DeliveryAddress;
                    textboxTrackingNumber.Text = o.TrackingNumber;
                    textboxCourier.Text = o.AssignedCourier;
                }
                else if (order is StoreOrder s)
                {
                    RadioStore.IsChecked = true;
                    textboxStoreLocation.Text = s.StoreLocation;
                    textboxPickupTime.Text = s.PickupTime;
                    textboxCourierStore.Text = s.AssignedCourier;
                }

                _isEditMode = true;
                FormTitle.Text = "Редагування замовлення";
                AddButton.Content = "💾 Зберегти зміни";
                CancelEditButton.Visibility = Visibility.Visible;
                ShowToast("✏ Режим редагування");
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
            ClearForm_Click(sender, e);
        }

        private void ExitEditMode()
        {
            _isEditMode = false;
            _editIndex = -1;
            FormTitle.Text = "Нове замовлення";
            AddButton.Content = "＋  Додати замовлення";
            CancelEditButton.Visibility = Visibility.Collapsed;
        }

        // ── Форма ─────────────────────────────────────────────────
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelOnline != null)
            {
                if (RadioOnline.IsChecked == true)
                {
                    PanelOnline.Visibility = Visibility.Visible;
                    PanelStore.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PanelOnline.Visibility = Visibility.Collapsed;
                    PanelStore.Visibility = Visibility.Visible;
                }
            }
        }

        private bool ValidateField(TextBox tb)
        {
            bool isValid;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.BorderBrush = new SolidColorBrush(Colors.Red);
                tb.BorderThickness = new Thickness(2);
                isValid = false;
            }
            else
            {
                tb.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#3A4A6A"));
                tb.BorderThickness = new Thickness(1.5);
                isValid = true;
            }
            return isValid;
        }

        private void AddRecord_Click(object sender, RoutedEventArgs e)
        {
            bool valid = ValidateField(textboxName);
            valid &= ValidateField(textboxQuantity);
            valid &= ValidateField(textboxPrice);

            if (!valid)
            {
                ShowToast("✘ Заповніть усі обов'язкові поля", false);
            }
            else if (!int.TryParse(textboxQuantity.Text, out int qty) || qty <= 0)
            {
                ShowToast("✘ Кількість має бути цілим додатнім числом", false);
            }
            else if (!double.TryParse(textboxPrice.Text, out double price) || price < 0)
            {
                ShowToast("✘ Ціна має бути невід'ємним числом", false);
            }
            else
            {
                try
                {
                    string createdAt = textboxCreatedAt.SelectedDate?.ToString("yyyy-MM-dd")
                                       ?? DateTime.Now.ToString("yyyy-MM-dd");

                    if (_isEditMode && _editIndex >= 0)
                    {
                        // Режим редагування — оновлюємо існуючий запис
                        OrderBase order;
                        if (RadioOnline.IsChecked == true)
                        {
                            var online = new OnlineOrder(
                                textboxName.Text.Trim(), qty, price,
                                textboxDeliveryAddress.Text.Trim(),
                                textboxTrackingNumber.Text.Trim());
                            online.AssignedCourier = textboxCourier.Text.Trim();
                            online.CreatedAt = createdAt;
                            order = online;
                        }
                        else
                        {
                            var store = new StoreOrder(
                                textboxName.Text.Trim(), qty, price,
                                textboxStoreLocation.Text.Trim(),
                                textboxPickupTime.Text.Trim());
                            store.AssignedCourier = textboxCourierStore.Text.Trim();
                            store.CreatedAt = createdAt;
                            order = store;
                        }

                        // Зберігаємо поточний статус
                        if (_manager[_editIndex] is IOrderTrackable oldT &&
                            order is IOrderTrackable newT)
                            newT.UpdateStatus(oldT.OrderState);

                        _manager[_editIndex] = order;
                        Orders[_editIndex] = new OrderViewModel(order, _editIndex);

                        Logger.Log("Редаговано", $"{order.ProductName}, {order.Quantity} шт., {order.Price:F2} грн");
                        ShowToast($"✔ Збережено: {order.ProductName}");
                        UpdateStatusBar($"Відредаговано: {order.ProductName}");
                        ExitEditMode();
                        ClearForm_Click(sender, e);
                    }
                    else
                    {
                        // Звичайний режим — додаємо новий запис
                        OrderBase order;
                        if (RadioOnline.IsChecked == true)
                        {
                            var online = new OnlineOrder(
                                textboxName.Text.Trim(), qty, price,
                                textboxDeliveryAddress.Text.Trim(),
                                textboxTrackingNumber.Text.Trim());
                            online.AssignedCourier = textboxCourier.Text.Trim();
                            online.CreatedAt = createdAt;
                            order = online;
                        }
                        else
                        {
                            var store = new StoreOrder(
                                textboxName.Text.Trim(), qty, price,
                                textboxStoreLocation.Text.Trim(),
                                textboxPickupTime.Text.Trim());
                            store.AssignedCourier = textboxCourierStore.Text.Trim();
                            store.CreatedAt = createdAt;
                            order = store;
                        }

                        _manager.Add(order);
                        Orders.Add(new OrderViewModel(order, _manager.Count - 1));
                        Logger.Log("Додано", $"{order.ProductName}, {order.Quantity} шт., {order.Price:F2} грн");
                        ShowToast($"✔ Додано: {order.ProductName}");
                        UpdateStatusBar($"Додано: {order.ProductName}");
                        ClearForm_Click(sender, e);
                    }

                    textboxCreatedAt.SelectedDate = DateTime.Now;
                }
                catch (System.ArgumentException ex)
                {
                    ShowToast($"✘ {ex.Message}", false);
                }
            }
        }

        private void UpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!(OrdersDataGrid.SelectedItem is OrderViewModel selectedVm))
            {
                ShowToast("✘ Оберіть запис у таблиці", false);
            }
            else if (!(StatusComboBox.SelectedItem is ComboBoxItem selectedStatus))
            {
                ShowToast("✘ Оберіть статус зі списку", false);
            }
            else
            {
                int index = Orders.IndexOf(selectedVm);
                var order = _manager[index];

                if (order is IOrderTrackable trackable)
                {
                    string old = trackable.OrderState;
                    trackable.UpdateStatus(selectedStatus.Content.ToString());
                    Orders[index] = new OrderViewModel(order, index);
                    Logger.Log("Змінено", $"Статус '{order.ProductName}': {old} → {trackable.OrderState}");
                    ShowToast($"✔ Статус оновлено: {trackable.OrderState}");
                    UpdateStatusBar($"Статус '{order.ProductName}' оновлено");
                }
            }
        }

        private void DeleteRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!(OrdersDataGrid.SelectedItem is OrderViewModel selected))
            {
                ShowToast("✘ Оберіть запис для видалення", false);
            }
            else
            {
                int index = Orders.IndexOf(selected);
                string name = selected.ProductName;
                _manager.RemoveAt(index);
                Orders.RemoveAt(index);
                Logger.Log("Видалено", $"Замовлення: {name}");
                ShowToast($"🗑 Видалено: {name}");
                UpdateStatusBar($"Видалено: {name}");
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            textboxName.Clear(); textboxQuantity.Clear(); textboxPrice.Clear();
            textboxDeliveryAddress.Clear(); textboxTrackingNumber.Clear();
            textboxCourier.Clear(); textboxStoreLocation.Clear();
            textboxPickupTime.Clear(); textboxCourierStore.Clear();
            textboxCreatedAt.SelectedDate = DateTime.Now;

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A4A6A"));
            foreach (var tb in new[] { textboxName, textboxQuantity, textboxPrice })
            {
                tb.BorderBrush = brush;
                tb.BorderThickness = new Thickness(1.5);
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem selected && Orders != null && _manager != null)
            {
                var pairs = new List<(OrderBase order, OrderViewModel vm)>();
                for (int i = 0; i < Orders.Count; i++)
                    pairs.Add((_manager[i], Orders[i]));

                string criterion = selected.Content.ToString();
                if (criterion == "Назвою товару")
                    pairs.Sort((a, b) => string.Compare(a.order.ProductName, b.order.ProductName));
                else if (criterion == "Кількістю")
                    pairs.Sort((a, b) => a.order.Quantity.CompareTo(b.order.Quantity));
                else if (criterion == "Ціною")
                    pairs.Sort((a, b) => a.order.Price.CompareTo(b.order.Price));
                else if (criterion == "Датою")
                    pairs.Sort((a, b) => string.Compare(a.order.CreatedAt, b.order.CreatedAt));
                else
                    pairs.Sort((a, b) => a.order.Total.CompareTo(b.order.Total));

                Orders.Clear();
                for (int i = 0; i < pairs.Count; i++)
                {
                    _manager[i] = pairs[i].order;
                    Orders.Add(pairs[i].vm);
                }
            }
        }




        private void ShowByIndex_Click(object sender, RoutedEventArgs e)
        {
            if (IndexResultBlock.Visibility == Visibility.Visible)
            {
                IndexResultBlock.Visibility = Visibility.Collapsed;
                IndexResultBlock.Text = "";
                ShowIndexButton.Content = "Показати";
            }
            else
            {
                if (int.TryParse(IndexTextBox.Text, out int idx))
                {
                    try
                    {
                        IndexResultBlock.Text = _manager[idx].GetDetails();
                        IndexResultBlock.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString("#C6A75E"));
                    }
                    catch (System.IndexOutOfRangeException ex)
                    {
                        IndexResultBlock.Text = ex.Message;
                        IndexResultBlock.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
                else
                {
                    IndexResultBlock.Text = "Введіть коректний індекс.";
                    IndexResultBlock.Foreground = new SolidColorBrush(Colors.Red);
                }

                IndexResultBlock.Visibility = Visibility.Visible;
                ShowIndexButton.Content = "Приховати";
            }
        }

        private void ShowByIndex_Click2(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(IndexSearchTextBox.Text, out int idx))
            {
                try
                {
                    SearchResultBlock.Text = _manager[idx].GetDetails();
                    SearchResultBorder.Visibility = Visibility.Visible;
                }
                catch (System.IndexOutOfRangeException ex)
                {
                    SearchResultBlock.Text = ex.Message;
                    SearchResultBorder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                SearchResultBlock.Text = "Введіть коректний індекс.";
                SearchResultBorder.Visibility = Visibility.Visible;
            }
        }

        private OrderBase GetOrderByIndex(string input, out string error)
        {
            OrderBase result;
            if (int.TryParse(input, out int index))
            {
                try
                {
                    result = _manager[index];
                    error = null;
                }
                catch (System.IndexOutOfRangeException ex)
                {
                    error = ex.Message;
                    result = null;
                }
            }
            else
            {
                error = "Введіть коректний індекс.";
                result = null;
            }
            return result;
        }

        private void OperatorAdd_Click(object sender, RoutedEventArgs e)
        {
            var a = GetOrderByIndex(IndexATextBox.Text, out string errA);
            var b = GetOrderByIndex(IndexBTextBox.Text, out string errB);
            if (errA != null) ShowOperatorResult(errA);
            else if (errB != null) ShowOperatorResult(errB);
            else ShowOperatorResult($"Результат (A + B):\n{(a + b).GetDetails()}");
        }

        private void OperatorSub_Click(object sender, RoutedEventArgs e)
        {
            var a = GetOrderByIndex(IndexATextBox.Text, out string errA);
            var b = GetOrderByIndex(IndexBTextBox.Text, out string errB);
            if (errA != null) ShowOperatorResult(errA);
            else if (errB != null) ShowOperatorResult(errB);
            else ShowOperatorResult($"Результат (A − B):\n{(a - b).GetDetails()}");
        }

        private void OperatorGreater_Click(object sender, RoutedEventArgs e)
        {
            var a = GetOrderByIndex(IndexATextBox.Text, out string errA);
            var b = GetOrderByIndex(IndexBTextBox.Text, out string errB);
            if (errA != null) ShowOperatorResult(errA);
            else if (errB != null) ShowOperatorResult(errB);
            else ShowOperatorResult((a > b)
                ? $"✔ A має вищу ціну ({a.Price:F2} грн > {b.Price:F2} грн)"
                : $"✘ A не має вищої ціни ({a.Price:F2} грн ≤ {b.Price:F2} грн)");
        }

        private void ShowOperatorResult(string text)
        {
            OperatorResultBlock.Text = text;
            OperatorResultBorder.Visibility = Visibility.Visible;
        }

        private void GenerateReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (!(OrdersDataGrid.SelectedItem is OrderViewModel selectedVm))
            {
                ShowToast("✘ Оберіть замовлення для формування чеку", false);
            }
            else
            {
                int index = Orders.IndexOf(selectedVm);
                var order = _manager[index];

                try
                {
                    string path = ReceiptGenerator.Generate(order);
                    Logger.Log("Чек", $"Сформовано PDF-чек для '{order.ProductName}'");
                    ShowToast($"✔ Чек збережено");
                    UpdateStatusBar($"Чек: {path}");
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowToast($"✘ Помилка генерації чеку: {ex.Message}", false);
                }
            }
        }
    }

    public class StatusBarItem
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public double BarWidth { get; set; }
    }

    public class OrderViewModel
    {
        private readonly OrderBase _order;
        private readonly int _index;

        public OrderViewModel(OrderBase order, int index)
        {
            _order = order;
            _index = index;
        }

        public string Index => $"#{_index}";
        public string ProductName => _order.ProductName;
        public int Quantity => _order.Quantity;
        public double Price => _order.Price;
        public double Total => _order.Total;
        public string CreatedAt => _order.CreatedAt;
        public string Details => _order.GetDetails().Replace("\n", "; ");
        public string OrderState => _order is IOrderTrackable t ? t.OrderState : "-";
        public string AssignedCourier => _order is IOrderTrackable t2 ? t2.AssignedCourier : "-";
        public string ReadyForPickup => _order is IOrderTrackable t3
                                         ? (t3.IsReadyForPickup() ? "✔" : "✘") : "-";
    }
}