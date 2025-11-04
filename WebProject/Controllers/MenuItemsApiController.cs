using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WebProject.Data;
using WebProject.Models;
using Google.Cloud.Storage.V1;
using System.Net.Http;
using System.IO;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;

namespace WebProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuItemsApiController : ControllerBase
    {
        private const string _bucketName = "restaurant-web-static-files";
        private readonly StorageClient _storageClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SearchClient _searchClient; 
        private readonly ApplicationDbContext _context;

        public MenuItemsApiController(StorageClient storageClient, 
            IHttpClientFactory httpClientFactory, ApplicationDbContext context, SearchClient searchClient)
        {
            _storageClient = storageClient;
            _httpClientFactory = httpClientFactory;
            _searchClient = searchClient;
            _context = context;
        }

        // GET: api/MenuItemsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems()
        {
            return await _context.MenuItems.Include(m => m.Restaurant).ToListAsync();
        }

        // GET: api/MenuItemsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MenuItem>> GetMenuItem(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);

            if (menuItem == null)
            {
                return NotFound();
            }

            return menuItem;
        }

        // PUT: api/MenuItemsApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMenuItem(int id, MenuItem menuItem)
        {
            if (id != menuItem.Id)
            {
                return BadRequest();
            }

            try
            {
                string newPhotoUrl = await IngestExternalPhotoAsync(menuItem.PhotoUrl);
                menuItem.PhotoUrl = newPhotoUrl;
            }
            catch (Exception ex)
            {
                
                return BadRequest(new { error = $"Не вдалося обробити URL зображення: {ex.Message}" });
            }


            _context.Entry(menuItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MenuItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok();
        }
        // POST: api/MenuItemsApi
        [HttpPost]
        public async Task<ActionResult<MenuItem>> PostMenuItem(MenuItem menuItem)
        {
            if (menuItem.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than zero");
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                string newPhotoUrl = await IngestExternalPhotoAsync(menuItem.PhotoUrl);
                menuItem.PhotoUrl = newPhotoUrl;
            }
            catch (Exception ex)
            {
                
                return BadRequest(new { error = $"Не вдалося обробити URL зображення: {ex.Message}" });
            }
           

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMenuItem", new { id = menuItem.Id }, menuItem);
        }

        // DELETE: api/MenuItemsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }

            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();

            return Ok();
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


        [HttpGet("search")] 
        public async Task<ActionResult<IEnumerable<MenuItemSearchDocument>>> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Пошуковий запит не може бути порожнім.");
            }

            try
            {
                SearchResults<MenuItemSearchDocument> results = 
                    await _searchClient.SearchAsync<MenuItemSearchDocument>(q);
                
                var documents = results.GetResults().Select(r => r.Document).ToList();
                
                return Ok(documents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Помилка пошуку: {ex.Message}" });
            }
        }
        
        [HttpPost("reindex")]
    public async Task<IActionResult> Reindex()
    {
    try
    {
        var allItems = await _context.MenuItems.Include(m => m.Restaurant).ToListAsync();

        var documents = allItems.Select(menuItem => new MenuItemSearchDocument
        {
            Id = menuItem.Id.ToString(),
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = (double)menuItem.Price,
            RestaurantName = menuItem.Restaurant?.Name ?? "Невідомо"
        }).ToList();

        if (documents.Any())
        {
            await _searchClient.UploadDocumentsAsync(documents);
        }

        return Ok(new { message = $"Синхронізовано {documents.Count} документів." });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = $"Помилка ре-індексації: {ex.Message}" });
    }
    }
    }
}