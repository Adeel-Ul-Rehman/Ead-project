/**
 * Professional Pagination & AJAX System
 * Handles pagination, AJAX operations, and DOM updates efficiently
 */

class PaginationManager {
    constructor(options) {
        this.container = options.container;
        this.endpoint = options.endpoint;
        this.pageSize = options.pageSize || 20;
        this.currentPage = 1;
        this.totalPages = 1;
        this.totalRecords = 0;
        this.filters = options.filters || {};
        this.onRenderItem = options.onRenderItem;
        this.onDataLoaded = options.onDataLoaded;
        this.itemIdPrefix = options.itemIdPrefix || 'item';
        
        this.init();
    }

    init() {
        this.loadPage(1);
    }

    async loadPage(page, showLoading = true) {
        if (showLoading) Loading.show('Loading data...');
        
        try {
            const params = new URLSearchParams({
                page: page,
                pageSize: this.pageSize,
                ...this.filters
            });

            const response = await fetch(`${this.endpoint}?${params}`, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Failed to load data');
            }

            const data = await response.json();
            
            this.currentPage = data.currentPage;
            this.totalPages = data.totalPages;
            this.totalRecords = data.totalRecords;
            
            this.renderItems(data.items);
            this.renderPagination();
            this.updateResultsInfo();
            
            if (this.onDataLoaded) {
                this.onDataLoaded(data);
            }
            
            // Scroll to top smoothly
            this.container.scrollIntoView({ behavior: 'smooth', block: 'start' });
            
        } catch (error) {
            console.error('Pagination error:', error);
            Toast.error('Failed to load data. Please try again.');
        } finally {
            if (showLoading) Loading.hide();
        }
    }

    renderItems(items) {
        const gridContainer = document.querySelector(this.container);
        if (!gridContainer) return;
        
        gridContainer.innerHTML = '';
        
        if (items.length === 0) {
            gridContainer.innerHTML = `
                <div class="col-span-full text-center py-12">
                    <svg class="w-16 h-16 mx-auto text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"></path>
                    </svg>
                    <p class="text-gray-600 dark:text-gray-400 text-lg font-medium">No records found</p>
                    <p class="text-gray-500 dark:text-gray-500 text-sm mt-2">Try adjusting your filters</p>
                </div>
            `;
            return;
        }
        
        items.forEach(item => {
            const itemHtml = this.onRenderItem(item);
            gridContainer.insertAdjacentHTML('beforeend', itemHtml);
        });
    }

    renderPagination() {
        const paginationContainer = document.getElementById('paginationControls');
        if (!paginationContainer) return;
        
        let html = `
            <div class="flex items-center justify-between bg-white dark:bg-gray-800 rounded-lg shadow-lg p-4">
                <div class="flex items-center gap-2">
                    <span class="text-sm text-gray-600 dark:text-gray-400">
                        Showing ${((this.currentPage - 1) * this.pageSize) + 1} to ${Math.min(this.currentPage * this.pageSize, this.totalRecords)} of ${this.totalRecords} records
                    </span>
                </div>
                
                <div class="flex items-center gap-2">
                    <!-- First Page -->
                    <button onclick="pagination.loadPage(1)" 
                            ${this.currentPage === 1 ? 'disabled' : ''}
                            class="px-3 py-2 text-sm font-medium rounded-lg ${this.currentPage === 1 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-gray-600'} border border-gray-300 dark:border-gray-600 transition duration-200">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7"></path>
                        </svg>
                    </button>
                    
                    <!-- Previous Page -->
                    <button onclick="pagination.loadPage(${this.currentPage - 1})" 
                            ${this.currentPage === 1 ? 'disabled' : ''}
                            class="px-3 py-2 text-sm font-medium rounded-lg ${this.currentPage === 1 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-gray-600'} border border-gray-300 dark:border-gray-600 transition duration-200">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
                        </svg>
                    </button>
                    
                    <!-- Page Numbers -->
                    <div class="hidden sm:flex items-center gap-2">
                        ${this.generatePageNumbers()}
                    </div>
                    
                    <!-- Next Page -->
                    <button onclick="pagination.loadPage(${this.currentPage + 1})" 
                            ${this.currentPage === this.totalPages ? 'disabled' : ''}
                            class="px-3 py-2 text-sm font-medium rounded-lg ${this.currentPage === this.totalPages ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-gray-600'} border border-gray-300 dark:border-gray-600 transition duration-200">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path>
                        </svg>
                    </button>
                    
                    <!-- Last Page -->
                    <button onclick="pagination.loadPage(${this.totalPages})" 
                            ${this.currentPage === this.totalPages ? 'disabled' : ''}
                            class="px-3 py-2 text-sm font-medium rounded-lg ${this.currentPage === this.totalPages ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-gray-600'} border border-gray-300 dark:border-gray-600 transition duration-200">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 5l7 7-7 7M5 5l7 7-7 7"></path>
                        </svg>
                    </button>
                </div>
                
                <!-- Page Size Selector -->
                <div class="flex items-center gap-2">
                    <label class="text-sm text-gray-600 dark:text-gray-400">Per page:</label>
                    <select onchange="pagination.changePageSize(this.value)" 
                            class="px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent dark:bg-gray-700 dark:text-white">
                        <option value="10" ${this.pageSize === 10 ? 'selected' : ''}>10</option>
                        <option value="20" ${this.pageSize === 20 ? 'selected' : ''}>20</option>
                        <option value="50" ${this.pageSize === 50 ? 'selected' : ''}>50</option>
                        <option value="100" ${this.pageSize === 100 ? 'selected' : ''}>100</option>
                    </select>
                </div>
            </div>
        `;
        
        paginationContainer.innerHTML = html;
    }

    generatePageNumbers() {
        let html = '';
        const maxVisible = 5;
        let startPage = Math.max(1, this.currentPage - Math.floor(maxVisible / 2));
        let endPage = Math.min(this.totalPages, startPage + maxVisible - 1);
        
        if (endPage - startPage < maxVisible - 1) {
            startPage = Math.max(1, endPage - maxVisible + 1);
        }
        
        if (startPage > 1) {
            html += `<span class="px-2 text-gray-500">...</span>`;
        }
        
        for (let i = startPage; i <= endPage; i++) {
            const isActive = i === this.currentPage;
            html += `
                <button onclick="pagination.loadPage(${i})" 
                        class="px-4 py-2 text-sm font-medium rounded-lg ${isActive ? 'bg-blue-600 text-white shadow-lg' : 'bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-blue-50 dark:hover:bg-gray-600'} border ${isActive ? 'border-blue-600' : 'border-gray-300 dark:border-gray-600'} transition duration-200">
                    ${i}
                </button>
            `;
        }
        
        if (endPage < this.totalPages) {
            html += `<span class="px-2 text-gray-500">...</span>`;
        }
        
        return html;
    }

    updateResultsInfo() {
        const infoElement = document.getElementById('resultsInfo');
        if (infoElement) {
            const from = ((this.currentPage - 1) * this.pageSize) + 1;
            const to = Math.min(this.currentPage * this.pageSize, this.totalRecords);
            infoElement.innerHTML = `
                <span class="font-semibold text-blue-600 dark:text-blue-400">${from}-${to}</span> of 
                <span class="font-semibold">${this.totalRecords}</span> records
            `;
        }
    }

    changePageSize(newSize) {
        this.pageSize = parseInt(newSize);
        this.loadPage(1);
    }

    updateFilters(newFilters) {
        this.filters = { ...this.filters, ...newFilters };
        this.loadPage(1);
    }

    refresh() {
        this.loadPage(this.currentPage, false);
    }

    addItem(itemHtml, position = 'top') {
        const gridContainer = document.querySelector(this.container);
        if (!gridContainer) return;
        
        // Remove "no records" message if exists
        const noRecords = gridContainer.querySelector('.col-span-full');
        if (noRecords) {
            noRecords.remove();
        }
        
        if (position === 'top') {
            gridContainer.insertAdjacentHTML('afterbegin', itemHtml);
        } else {
            gridContainer.insertAdjacentHTML('beforeend', itemHtml);
        }
        
        // Update total records
        this.totalRecords++;
        this.updateResultsInfo();
        
        // Add fade-in animation
        const newItem = position === 'top' ? 
            gridContainer.firstElementChild : 
            gridContainer.lastElementChild;
        
        if (newItem) {
            newItem.style.opacity = '0';
            newItem.style.transform = 'translateY(-10px)';
            setTimeout(() => {
                newItem.style.transition = 'all 0.3s ease-out';
                newItem.style.opacity = '1';
                newItem.style.transform = 'translateY(0)';
            }, 10);
        }
    }

    removeItem(itemId) {
        const item = document.getElementById(itemId);
        if (!item) return;
        
        // Fade out animation
        item.style.transition = 'all 0.3s ease-out';
        item.style.opacity = '0';
        item.style.transform = 'translateX(20px)';
        
        setTimeout(() => {
            item.remove();
            this.totalRecords--;
            this.updateResultsInfo();
            
            // Check if page is empty and reload
            const gridContainer = document.querySelector(this.container);
            if (gridContainer && gridContainer.children.length === 0) {
                this.loadPage(Math.max(1, this.currentPage - 1));
            }
        }, 300);
    }

    updateItem(itemId, newHtml) {
        const item = document.getElementById(itemId);
        if (!item) return;
        
        // Flash animation
        item.style.transition = 'all 0.3s ease-out';
        item.style.backgroundColor = '#dbeafe'; // Light blue
        
        setTimeout(() => {
            item.outerHTML = newHtml;
            const updatedItem = document.getElementById(itemId);
            if (updatedItem) {
                setTimeout(() => {
                    updatedItem.style.backgroundColor = '';
                }, 300);
            }
        }, 300);
    }
}

// Debounce utility for search inputs
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Export for use in other scripts
window.PaginationManager = PaginationManager;
window.debounce = debounce;
