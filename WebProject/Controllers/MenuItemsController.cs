using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WebProject.Data;
using WebProject.Models;
using Google.Cloud.Storage.V1;
using System.Net.Http; 
using System.IO; 

namespace WebProject.Controllers
{
    public class MenuItemsController : Controller
    {
        private const string _bucketName = "restaurant-web-static-files";
        private readonly StorageClient _storageClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;

        public MenuItemsController(StorageClient storageClient, 
            IHttpClientFactory httpClientFactory, ApplicationDbContext context)
        {
            _storageClient = storageClient;
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        // GET: MenuItems
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.MenuItems.Include(m => m.Restaurant);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: MenuItems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _context.MenuItems
                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // GET: MenuItems/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name");
            return View();
        }

        // POST: MenuItems/Create
        // ...
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RestaurantId,Name,Price,Description,PhotoUrl")] MenuItem menuItem)
        {
            if (menuItem.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than zero");
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    string newPhotoUrl = await IngestExternalPhotoAsync(menuItem.PhotoUrl);
                    
                    menuItem.PhotoUrl = newPhotoUrl;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("PhotoUrl", $"Не вдалося обробити URL зображення: {ex.Message}");
                    ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name", menuItem.RestaurantId);
                    return View(menuItem);
                }
                
                _context.Add(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name", menuItem.RestaurantId);
            return View(menuItem);
        }

        // GET: MenuItems/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }
            ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name", menuItem.RestaurantId);
            return View(menuItem);
        }

        // POST: MenuItems/Edit/5
        // ...
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RestaurantId,Name,Price,Description,PhotoUrl")] MenuItem menuItem)
        {
            if (id != menuItem.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    string newPhotoUrl = await IngestExternalPhotoAsync(menuItem.PhotoUrl);
                    
                    menuItem.PhotoUrl = newPhotoUrl;
                    
                    _context.Update(menuItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuItemExists(menuItem.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex) 
                {
                    ModelState.AddModelError("PhotoUrl", $"Не вдалося обробити URL зображення: {ex.Message}");
                    ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name", menuItem.RestaurantId);
                    return View(menuItem);
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["RestaurantId"] = new SelectList(_context.Restaurants, "Id", "Name", menuItem.RestaurantId);
            return View(menuItem);
        }

        // GET: MenuItems/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _context.MenuItems
                .Include(m => m.Restaurant)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // POST: MenuItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem != null)
            {
                _context.MenuItems.Remove(menuItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }
        
        private async Task<string> IngestExternalPhotoAsync(string externalUrl)
        {
            if (string.IsNullOrEmpty(externalUrl) || 
                !Uri.IsWellFormedUriString(externalUrl, UriKind.Absolute) ||
                externalUrl.Contains("storage.googleapis.com"))
            {
                return externalUrl;
            }

            var httpClient = _httpClientFactory.CreateClient();
            
            using var response = await httpClient.GetAsync(externalUrl);
            response.EnsureSuccessStatusCode(); 
            
            using var imageStream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            var extension = Path.GetExtension(new Uri(externalUrl).AbsolutePath);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg"; 
            var fileName = $"{Guid.NewGuid()}{extension}";

            await _storageClient.UploadObjectAsync(
                _bucketName,
                fileName,
                contentType,
                imageStream
            );

            var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{fileName}";
            
            return publicUrl;
        }
    }
}