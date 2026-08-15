"use strict";

/*
    商品詳細頁的加入購物車功能。

    和商品卡不同：

    商品卡：
    quantity 永遠 = 1

    詳細頁：
    quantity 由 input 決定。
*/


// ==========================================
// 取得 DOM Elements
// ==========================================

const addCartButton =
    document.querySelector(
        "#product-detail-add-cart"
    );


const quantityInput =
    document.querySelector(
        "#product-quantity"
    );


const feedback =
    document.querySelector(
        "#product-detail-cart-feedback"
    );


/*
    這個 JavaScript 只會在 Details Page 載入。

    但仍然確認 element 存在，
    避免 DOM 結構改變後直接出現 JS Error。
*/
if (addCartButton &&
    quantityInput) {

    addCartButton.addEventListener(
        "click",
        async () => {

            // ==================================
            // 1. ProductId
            // ==================================

            const productId =
                Number(
                    addCartButton.dataset.productId
                );


            // ==================================
            // 2. Stock
            // ==================================

            const stock =
                Number(
                    addCartButton.dataset.productStock
                );


            // ==================================
            // 3. Quantity
            // ==================================

            const quantity =
                Number(
                    quantityInput.value
                );


            // ==================================
            // 4. Browser-side validation
            // ==================================

            /*
                注意：

                JavaScript Validation
                只是讓 UI 更快提示。

                真正不能信任 Client。

                Step 6 API 還是會重新：

                DataAnnotations Validation
                +
                DB Stock Validation
            */

            if (!Number.isInteger(quantity) ||
                quantity < 1) {

                showDetailFeedback(
                    "數量必須大於或等於 1。",
                    true
                );

                return;
            }


            if (quantity > stock) {

                showDetailFeedback(
                    `數量不可超過目前庫存 ${stock}。`,
                    true
                );

                return;
            }


            if (!Number.isInteger(productId) ||
                productId <= 0) {

                showDetailFeedback(
                    "商品編號無效。",
                    true
                );

                return;
            }


            // ==================================
            // 5. Disable Button
            // ==================================

            addCartButton.disabled =
                true;


            const originalText =
                addCartButton.textContent.trim();


            addCartButton.textContent =
                "加入中...";


            showDetailFeedback(
                "",
                false
            );


            try {

                // ==============================
                // 6. POST Cart API
                // ==============================

                const response =
                    await fetch(
                        "/api/cart/items",
                        {
                            method: "POST",

                            headers: {
                                "Content-Type":
                                    "application/json"
                            },

                            credentials:
                                "same-origin",

                            body:
                                JSON.stringify({
                                    productId:
                                        productId,

                                    quantity:
                                        quantity
                                })
                        }
                    );


                // ==============================
                // 7. API Error
                // ==============================

                if (!response.ok) {

                    const message =
                        await getDetailErrorMessage(
                            response
                        );

                    throw new Error(
                        message
                    );
                }


                // ==============================
                // 8. API Success
                // ==============================

                const cart =
                    await response.json();


                showDetailFeedback(
                    `已加入 ${quantity} 件，目前購物車共 ${cart.totalQuantity} 件商品。`,
                    false
                );

            }
            catch (error) {

                console.error(
                    "加入購物車失敗：",
                    error
                );


                showDetailFeedback(
                    error instanceof Error
                        ? error.message
                        : "加入購物車失敗。",
                    true
                );

            }
            finally {

                addCartButton.disabled =
                    false;


                addCartButton.textContent =
                    originalText;
            }
        }
    );
}


// ==========================================
// Feedback
// ==========================================

function showDetailFeedback(
    message,
    isError
) {
    if (!feedback) {
        return;
    }


    feedback.textContent =
        message;


    feedback.classList.toggle(
        "cart-feedback--error",
        isError
    );


    feedback.classList.toggle(
        "cart-feedback--success",
        !isError && message !== ""
    );
}


// ==========================================
// Error Response
// ==========================================

async function getDetailErrorMessage(
    response
) {
    try {

        const data =
            await response.json();


        if (typeof data.message === "string") {
            return data.message;
        }


        if (typeof data.detail === "string") {
            return data.detail;
        }


        if (data.errors &&
            typeof data.errors === "object") {

            const errorGroups =
                Object.values(
                    data.errors
                );


            for (const group of errorGroups) {

                if (Array.isArray(group) &&
                    group.length > 0) {

                    return String(
                        group[0]
                    );
                }
            }
        }

    }
    catch {
        // Response 不是 JSON 時改用通用訊息。
    }


    return `加入購物車失敗（HTTP ${response.status}）。`;
}
