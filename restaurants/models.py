from django.conf import settings
from django.db import models
from django.utils import timezone

class Restaurant(models.Model):
    name = models.CharField(max_length=200)
    address = models.TextField()
    photo = models.ImageField(upload_to='restaurants/photos/', null=True, blank=True)

    def __str__(self):
        return self.name

    class Meta:
        ordering = ['name']


class Table(models.Model):
    restaurant = models.ForeignKey(Restaurant, on_delete=models.CASCADE, related_name='tables')
    number = models.CharField(max_length=20) 
    seats = models.PositiveSmallIntegerField()

    class Meta:
        unique_together = ('restaurant', 'number')
        ordering = ['restaurant', 'number']

    def __str__(self):
        return f'{self.restaurant.name} — Table {self.number} ({self.seats})'


class Customer(models.Model):
    """
    Профіль клієнта. Може бути пов'язаний з user (auth.User) якщо є логін.
    """
    user = models.OneToOneField(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.SET_NULL,
        related_name='customer_profile'
    )
    name = models.CharField(max_length=200)
    phone = models.CharField(max_length=30)

    def __str__(self):
        return self.name


class MenuItem(models.Model):
    restaurant = models.ForeignKey(Restaurant, on_delete=models.CASCADE, related_name='menu_items')
    name = models.CharField(max_length=200)
    description = models.TextField(blank=True)
    price = models.DecimalField(max_digits=8, decimal_places=2)
    photo = models.ImageField(upload_to='menu/photos/', null=True, blank=True)
    available = models.BooleanField(default=True)

    def __str__(self):
        return f'{self.name} — {self.restaurant.name}'


class Order(models.Model):
    """
    Замовлення користувача. Поле is_table_booking == True => бронювання столика,
    інакше => замовлення на виніс.
    """
    STATUS_CHOICES = [
        ('PENDING', 'Очікує'),
        ('CONFIRMED', 'Підтверджено'),
        ('PREPARING', 'Готується'),
        ('READY', 'Готово'),
        ('COMPLETED', 'Виконано'),
        ('CANCELLED', 'Скасовано'),
    ]

    customer = models.ForeignKey(Customer, on_delete=models.SET_NULL, null=True, blank=True, related_name='orders')
    restaurant = models.ForeignKey(Restaurant, on_delete=models.CASCADE, related_name='orders')
    table = models.ForeignKey(Table, on_delete=models.SET_NULL, null=True, blank=True, related_name='orders')
    created_at = models.DateTimeField(default=timezone.now)
    scheduled_for = models.DateTimeField(null=True, blank=True)  # дата/час бронювання або очікування самовивозу
    number_of_people = models.PositiveSmallIntegerField(null=True, blank=True)
    is_table_booking = models.BooleanField(default=True)  # True => бронювання столика, False => виніс
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='PENDING')
    notes = models.TextField(blank=True)
    contact_phone = models.CharField(max_length=30, blank=True)  # якщо гість без аккаунту
    delivery_address = models.TextField(blank=True)  # для доставки/виніс (опц.)

    # збережений підсумковий прайс (snapshot): заповнюється при підтвердженні
    total_amount = models.DecimalField(max_digits=10, decimal_places=2, default=0)

    def __str__(self):
        return f'Order #{self.id} — {self.restaurant.name} — {self.get_status_display()}'

    class Meta:
        ordering = ['-created_at']

    def calculate_total(self):
        total = sum(item.line_total() for item in self.items.all())
        self.total_amount = total
        return total


class OrderItem(models.Model):
    """
    Зв'язок замовлення і страв(лівих) — тут зберігаємо кількість та ціну на момент замовлення.
    """
    order = models.ForeignKey(Order, on_delete=models.CASCADE, related_name='items')
    menu_item = models.ForeignKey(MenuItem, on_delete=models.PROTECT)
    quantity = models.PositiveIntegerField(default=1)
    price_at_order = models.DecimalField(max_digits=8, decimal_places=2)

    class Meta:
        ordering = ['id']

    def __str__(self):
        return f'{self.menu_item.name} x{self.quantity}'

    def line_total(self):
        return self.price_at_order * self.quantity
