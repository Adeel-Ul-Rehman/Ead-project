/**
 * AJAX Form Handler - Prevents page reload on form submissions
 * Handles form submissions asynchronously with error handling
 */

// Toast notification utility
const Toast = {
    success(message, duration = 3000) {
        this.show(message, 'success', duration);
    },
    error(message, duration = 5000) {
        this.show(message, 'error', duration);
    },
    info(message, duration = 3000) {
        this.show(message, 'info', duration);
    },
    show(message, type = 'info', duration = 3000) {
        // Remove existing toasts
        const existing = document.getElementById('toast-notification');
        if (existing) existing.remove();

        // Create toast element
        const toast = document.createElement('div');
        toast.id = 'toast-notification';
        toast.className = `fixed top-4 right-4 z-[9999] max-w-md w-full sm:w-96 transform transition-all duration-300 ease-in-out`;
        
        const bgColor = type === 'success' ? 'bg-green-500' : 
                       type === 'error' ? 'bg-red-500' : 
                       type === 'info' ? 'bg-blue-500' : 'bg-gray-500';
        
        const icon = type === 'success' ? '✓' : 
                    type === 'error' ? '✕' : 
                    type === 'info' ? 'ℹ' : '•';

        toast.innerHTML = `
            <div class="${bgColor} text-white px-6 py-4 rounded-lg shadow-2xl flex items-center justify-between">
                <div class="flex items-center space-x-3">
                    <span class="text-2xl font-bold">${icon}</span>
                    <p class="text-sm font-medium">${message}</p>
                </div>
                <button onclick="this.closest('#toast-notification').remove()" 
                        class="ml-4 text-white hover:text-gray-200 transition">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                    </svg>
                </button>
            </div>
        `;

        document.body.appendChild(toast);

        // Animate in
        setTimeout(() => toast.classList.add('translate-x-0'), 10);

        // Auto remove
        setTimeout(() => {
            toast.classList.add('translate-x-full', 'opacity-0');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    }
};

// Loading overlay utility
const Loading = {
    show(message = 'Processing...') {
        const existing = document.getElementById('loading-overlay');
        if (existing) return;

        const overlay = document.createElement('div');
        overlay.id = 'loading-overlay';
        overlay.className = 'fixed inset-0 bg-black bg-opacity-50 z-[9998] flex items-center justify-center';
        overlay.innerHTML = `
            <div class="bg-white dark:bg-gray-800 rounded-lg p-6 shadow-2xl flex flex-col items-center space-y-3">
                <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
                <p class="text-gray-700 dark:text-gray-300 font-medium">${message}</p>
            </div>
        `;
        document.body.appendChild(overlay);
    },
    hide() {
        const overlay = document.getElementById('loading-overlay');
        if (overlay) overlay.remove();
    }
};

// AJAX Form Handler
class AjaxForm {
    constructor(formElement, options = {}) {
        this.form = formElement;
        this.options = {
            onSuccess: options.onSuccess || null,
            onError: options.onError || null,
            showLoading: options.showLoading !== false,
            showToast: options.showToast !== false,
            loadingMessage: options.loadingMessage || 'Processing...',
            resetForm: options.resetForm || false,
            ...options
        };
        this.init();
    }

    init() {
        this.form.addEventListener('submit', (e) => this.handleSubmit(e));
    }

    async handleSubmit(e) {
        e.preventDefault();

        if (this.options.showLoading) {
            Loading.show(this.options.loadingMessage);
        }

        try {
            const formData = new FormData(this.form);
            const method = this.form.method || 'POST';
            const url = this.form.action;

            const response = await fetch(url, {
                method: method,
                body: formData,
                headers: {
                    'RequestVerificationToken': formData.get('__RequestVerificationToken')
                }
            });

            Loading.hide();

            if (response.ok) {
                const contentType = response.headers.get('content-type');
                let result;

                if (contentType && contentType.includes('application/json')) {
                    result = await response.json();
                } else {
                    result = { success: true, message: 'Operation completed successfully' };
                }

                if (this.options.showToast) {
                    Toast.success(result.message || 'Success!');
                }

                if (this.options.resetForm) {
                    this.form.reset();
                }

                if (this.options.onSuccess) {
                    this.options.onSuccess(result);
                }
            } else {
                const error = await response.text();
                throw new Error(error || 'Operation failed');
            }
        } catch (error) {
            Loading.hide();
            console.error('Form submission error:', error);
            
            if (this.options.showToast) {
                Toast.error(error.message || 'An error occurred. Please try again.');
            }

            if (this.options.onError) {
                this.options.onError(error);
            }
        }
    }
}

// Initialize AJAX forms automatically
document.addEventListener('DOMContentLoaded', function() {
    // Auto-initialize forms with data-ajax="true"
    document.querySelectorAll('form[data-ajax="true"]').forEach(form => {
        new AjaxForm(form, {
            onSuccess: (result) => {
                // Reload page section if specified
                const reloadTarget = form.getAttribute('data-ajax-reload');
                if (reloadTarget) {
                    const target = document.querySelector(reloadTarget);
                    if (target && result.html) {
                        target.innerHTML = result.html;
                    }
                }

                // Redirect if specified
                const redirect = form.getAttribute('data-ajax-redirect');
                if (redirect) {
                    setTimeout(() => window.location.href = redirect, 1000);
                }

                // Trigger custom event
                form.dispatchEvent(new CustomEvent('ajax-success', { detail: result }));
            }
        });
    });

    // Handle delete confirmations with AJAX
    document.querySelectorAll('[data-ajax-delete]').forEach(button => {
        button.addEventListener('click', async function(e) {
            e.preventDefault();
            
            const url = this.getAttribute('data-ajax-delete');
            const confirmMsg = this.getAttribute('data-confirm') || 'Are you sure you want to delete this item?';
            
            if (!confirm(confirmMsg)) return;

            Loading.show('Deleting...');

            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const response = await fetch(url, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': token,
                        'Content-Type': 'application/json'
                    }
                });

                Loading.hide();

                if (response.ok) {
                    Toast.success('Deleted successfully');
                    
                    // Remove element from DOM
                    const removeTarget = this.getAttribute('data-remove-target');
                    if (removeTarget) {
                        const element = document.querySelector(removeTarget);
                        if (element) {
                            element.style.transition = 'opacity 0.3s';
                            element.style.opacity = '0';
                            setTimeout(() => element.remove(), 300);
                        }
                    } else {
                        // Remove closest row/card
                        const row = this.closest('tr, .card, [data-item]');
                        if (row) {
                            row.style.transition = 'opacity 0.3s';
                            row.style.opacity = '0';
                            setTimeout(() => row.remove(), 300);
                        }
                    }
                } else {
                    throw new Error('Delete operation failed');
                }
            } catch (error) {
                Loading.hide();
                Toast.error(error.message || 'Failed to delete. Please try again.');
            }
        });
    });
});

// Export for global use
window.AjaxForm = AjaxForm;
window.Toast = Toast;
window.Loading = Loading;
