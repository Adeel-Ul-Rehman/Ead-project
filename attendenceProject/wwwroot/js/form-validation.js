// Form Validation Utilities

// Validate form fields with visual feedback
function validateForm(formId) {
    const form = document.getElementById(formId);
    if (!form) return false;

    let isValid = true;
    const requiredFields = form.querySelectorAll('[required]');

    requiredFields.forEach(field => {
        if (!field.value.trim()) {
            showFieldError(field, 'This field is required');
            isValid = false;
        } else {
            clearFieldError(field);
        }
    });

    // Email validation
    form.querySelectorAll('input[type="email"]').forEach(field => {
        if (field.value && !isValidEmail(field.value)) {
            showFieldError(field, 'Please enter a valid email address');
            isValid = false;
        }
    });

    return isValid;
}

// Show error message below field
function showFieldError(field, message) {
    clearFieldError(field);
    
    field.classList.add('border-red-500', 'bg-red-50');
    field.classList.remove('border-gray-300', 'border-blue-500');
    
    const errorDiv = document.createElement('div');
    errorDiv.className = 'field-error text-red-600 text-sm mt-1 flex items-center gap-1';
    errorDiv.innerHTML = `
        <svg class="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/>
        </svg>
        <span>${message}</span>
    `;
    
    field.parentElement.appendChild(errorDiv);
    field.setAttribute('aria-invalid', 'true');
    field.setAttribute('aria-describedby', 'error-' + field.id);
}

// Clear error state from field
function clearFieldError(field) {
    field.classList.remove('border-red-500', 'bg-red-50');
    field.classList.add('border-gray-300');
    field.removeAttribute('aria-invalid');
    field.removeAttribute('aria-describedby');
    
    const parent = field.parentElement;
    const existingError = parent.querySelector('.field-error');
    if (existingError) {
        existingError.remove();
    }
}

// Email validation
function isValidEmail(email) {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(email);
}

// Real-time validation on input
function enableRealTimeValidation(formId) {
    const form = document.getElementById(formId);
    if (!form) return;

    form.querySelectorAll('input, textarea, select').forEach(field => {
        field.addEventListener('input', function() {
            if (this.hasAttribute('aria-invalid')) {
                // Re-validate if field was previously invalid
                if (this.value.trim()) {
                    clearFieldError(this);
                    
                    // Check email format
                    if (this.type === 'email' && !isValidEmail(this.value)) {
                        showFieldError(this, 'Please enter a valid email address');
                    }
                }
            }
        });

        field.addEventListener('blur', function() {
            // Validate on blur
            if (this.hasAttribute('required') && !this.value.trim()) {
                showFieldError(this, 'This field is required');
            } else if (this.type === 'email' && this.value && !isValidEmail(this.value)) {
                showFieldError(this, 'Please enter a valid email address');
            } else {
                clearFieldError(this);
            }
        });
    });
}

// Confirm dialog for delete/destructive actions
function confirmAction(message, callback) {
    const modal = document.createElement('div');
    modal.className = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[9999]';
    modal.innerHTML = `
        <div class="bg-white dark:bg-gray-800 rounded-lg shadow-2xl p-6 max-w-md w-full mx-4 animate-fadeIn">
            <div class="flex items-start gap-4 mb-4">
                <div class="flex-shrink-0 w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 flex items-center justify-center">
                    <svg class="w-6 h-6 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
                    </svg>
                </div>
                <div>
                    <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">Confirm Action</h3>
                    <p class="text-sm text-gray-600 dark:text-gray-400">${message}</p>
                </div>
            </div>
            <div class="flex gap-3 justify-end mt-6">
                <button onclick="this.closest('.fixed').remove()" class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-800 dark:text-gray-200 rounded hover:bg-gray-300 dark:hover:bg-gray-600 transition font-medium">
                    Cancel
                </button>
                <button id="confirmBtn" class="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 active:bg-red-700 transition font-medium shadow-sm hover:shadow">
                    Confirm
                </button>
            </div>
        </div>
    `;
    
    document.body.appendChild(modal);
    
    modal.querySelector('#confirmBtn').addEventListener('click', function() {
        modal.remove();
        callback();
    });
    
    modal.addEventListener('click', function(e) {
        if (e.target === modal) {
            modal.remove();
        }
    });
}

// Success confirmation
function showSuccess(message, callback) {
    const modal = document.createElement('div');
    modal.className = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[9999]';
    modal.innerHTML = `
        <div class="bg-white dark:bg-gray-800 rounded-lg shadow-2xl p-6 max-w-md w-full mx-4 animate-fadeIn">
            <div class="flex flex-col items-center text-center gap-4">
                <div class="w-16 h-16 rounded-full bg-green-100 dark:bg-green-900 flex items-center justify-center">
                    <svg class="w-10 h-10 text-green-600 dark:text-green-400" fill="currentColor" viewBox="0 0 20 20">
                        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                    </svg>
                </div>
                <div>
                    <h3 class="text-xl font-semibold text-gray-900 dark:text-white mb-2">Success!</h3>
                    <p class="text-sm text-gray-600 dark:text-gray-400">${message}</p>
                </div>
                <button onclick="this.closest('.fixed').remove()" class="mt-2 px-6 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600 transition font-medium">
                    OK
                </button>
            </div>
        </div>
    `;
    
    document.body.appendChild(modal);
    
    if (callback) {
        modal.querySelector('button').addEventListener('click', callback);
    }
}

// Initialize validation on page load
document.addEventListener('DOMContentLoaded', function() {
    // Enable real-time validation for all forms with data-validate attribute
    document.querySelectorAll('form[data-validate]').forEach(form => {
        enableRealTimeValidation(form.id);
        
        form.addEventListener('submit', function(e) {
            if (!validateForm(form.id)) {
                e.preventDefault();
                showToast('Please fix the errors before submitting', 'error');
            }
        });
    });
    
    // Add confirmation to delete buttons
    document.querySelectorAll('[data-confirm]').forEach(button => {
        button.addEventListener('click', function(e) {
            e.preventDefault();
            const message = this.dataset.confirm || 'Are you sure you want to proceed?';
            const form = this.closest('form');
            
            confirmAction(message, function() {
                if (form) {
                    form.submit();
                } else if (button.href) {
                    window.location.href = button.href;
                }
            });
        });
    });
});

// Add fadeIn animation
const style = document.createElement('style');
style.textContent = `
    @keyframes fadeIn {
        from {
            opacity: 0;
            transform: scale(0.95);
        }
        to {
            opacity: 1;
            transform: scale(1);
        }
    }
    .animate-fadeIn {
        animation: fadeIn 0.2s ease-out;
    }
`;
document.head.appendChild(style);
