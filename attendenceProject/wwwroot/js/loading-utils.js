// Loading Indicator Utilities

// Show loading overlay on entire page
function showPageLoading(message = 'Loading...') {
    const loadingDiv = document.getElementById('pageLoading') || createPageLoading();
    const messageEl = loadingDiv.querySelector('.loading-message');
    if (messageEl) messageEl.textContent = message;
    loadingDiv.classList.remove('hidden');
}

// Hide loading overlay
function hidePageLoading() {
    const loadingDiv = document.getElementById('pageLoading');
    if (loadingDiv) loadingDiv.classList.add('hidden');
}

// Create page loading overlay
function createPageLoading() {
    const div = document.createElement('div');
    div.id = 'pageLoading';
    div.className = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[9999] hidden';
    div.innerHTML = `
        <div class="bg-white dark:bg-gray-800 rounded-lg shadow-2xl p-8 flex flex-col items-center gap-4">
            <div class="relative w-16 h-16">
                <div class="absolute top-0 left-0 w-full h-full border-4 border-blue-200 rounded-full"></div>
                <div class="absolute top-0 left-0 w-full h-full border-4 border-blue-600 rounded-full border-t-transparent animate-spin"></div>
            </div>
            <p class="loading-message text-gray-700 dark:text-gray-300 font-medium">Loading...</p>
        </div>
    `;
    document.body.appendChild(div);
    return div;
}

// Show loading button state
function setButtonLoading(button, loading = true) {
    if (loading) {
        button.dataset.originalText = button.innerHTML;
        button.disabled = true;
        button.classList.add('opacity-75', 'cursor-not-allowed');
        button.innerHTML = `
            <svg class="animate-spin h-5 w-5 mx-auto" viewBox="0 0 24 24" fill="none">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
        `;
    } else {
        button.disabled = false;
        button.classList.remove('opacity-75', 'cursor-not-allowed');
        if (button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
        }
    }
}

// Add loading to form submission
function addFormLoadingHandler(formId, message = 'Submitting...') {
    const form = document.getElementById(formId);
    if (form) {
        form.addEventListener('submit', function() {
            showPageLoading(message);
        });
    }
}

// Show inline loading spinner
function showInlineLoading(elementId) {
    const el = document.getElementById(elementId);
    if (el) {
        el.innerHTML = `
            <div class="flex items-center justify-center py-8">
                <div class="relative w-12 h-12">
                    <div class="absolute top-0 left-0 w-full h-full border-4 border-blue-200 rounded-full"></div>
                    <div class="absolute top-0 left-0 w-full h-full border-4 border-blue-600 rounded-full border-t-transparent animate-spin"></div>
                </div>
            </div>
        `;
    }
}

// Initialize all forms with loading
document.addEventListener('DOMContentLoaded', function() {
    // Add loading to all forms with data-loading attribute
    document.querySelectorAll('form[data-loading]').forEach(form => {
        const message = form.dataset.loading || 'Processing...';
        form.addEventListener('submit', function() {
            showPageLoading(message);
        });
    });

    // Add loading to all buttons with data-loading attribute
    document.querySelectorAll('button[data-loading]').forEach(button => {
        button.addEventListener('click', function() {
            if (this.type !== 'submit') {
                setButtonLoading(this);
            }
        });
    });
});
