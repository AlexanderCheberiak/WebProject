from django.urls import path, include
from rest_framework.routers import DefaultRouter
#from .views import RestaurantViewSet, MenuItemViewSet, TableViewSet, OrderViewSet
from . import views

router = DefaultRouter()
router.register(r'restaurants', views.RestaurantViewSet)
router.register(r'tables', views.TableViewSet)
router.register(r'menu-items', views.MenuItemViewSet)
router.register(r'orders', views.OrderViewSet)
router.register(r'order-items', views.OrderItemViewSet)

urlpatterns = [
    path('', include(router.urls)),
]