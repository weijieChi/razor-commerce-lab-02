"use strict";

/*
    商品卡加入購物車。

    這支 JavaScript 會處理所有：

    .js-product-card-add-cart

    按鈕。

    因此：

    首頁商品卡
    商品列表商品卡
    詳細頁下方推薦商品卡

    都可以共用。
*/


// ==========================================
// 找出所有商品卡按鈕
// ==========================================

const productCardButtons =
    document.querySelectorAll(
        ".js-product-card-add-cart"
    );


/*
    querySelectorAll() 回傳多個元素。

    所以使用 forEach()
    幫每個按鈕註冊 click event。
*/
productCardButtons.forEach(button => {

    button.addEventListener(
        "click",
        async () => {

            // ==================================
            // 1. 取得 ProductId
            // ==================================

            /*
                HTML：

                data-product-id="5"

                ↓

                JavaScript：

                button.dataset.productId

                ↓

                "5"
            */
            const productId =
                Number(
                    button.dataset.productId
                );


            /*
                closest(".product-card")

                從目前按鈕往父層找
                最近的 .product-card。
            */
            const productCard =
                button.closest(
                    ".product-card"
                );


            /*
                找這一張商品卡自己的訊息區。

                不會影響其他商品卡。
            */
            const feedback =
                productCard?.querySelector(
                    ".cart-feedback"
                );


            // ==================================
            // 2. 基本安全檢查
            // ==================================

            if (!Number.isInteger(productId) ||
                productId <= 0) {

                showFeedback(
                    feedback,
                    "商品編號無效。",
                    true
                );

                return;
            }


            // ==================================
            // 3. 防止重複點擊
            // ==================================

            /*
                假設使用者快速按：

                click
                click
                click

                可能瞬間送出三個 POST。

                所以 request 執行期間
                暫時 disable button。
            */
            button.disabled = true;


            /*
                保存原本按鈕文字。

                Request 結束後可以恢復。
            */
            const originalText =
                button.textContent.trim();


            button.textContent =
                "加入中...";


            /*
                清除前一次訊息。
            */
            showFeedback(
                feedback,
                "",
                false
            );


            try {

                // ==============================
                // 4. 呼叫購物車 API
                // ==============================

                const response =
                    await fetch(
                        "/api/cart/items",
                        {
                            method: "POST",

                            /*
                                告訴 Server：

                                Request Body 是 JSON。
                            */
                            headers: {
                                "Content-Type":
                                    "application/json"
                            },


                            /*
                                Step 6 的匿名購物車
                                使用 Cookie 保存 CartKey。

                                same-origin：

                                同一個網站的 request
                                要帶 Cookie。
                            */
                            credentials:
                                "same-origin",


                            /*
                                JavaScript Object：

                                {
                                    productId: 5,
                                    quantity: 1
                                }

                                ↓ JSON.stringify()

                                JSON：

                                {
                                  "productId": 5,
                                  "quantity": 1
                                }
                            */
                            body:
                                JSON.stringify({
                                    productId:
                                        productId,

                                    /*
                                        商品卡固定加入 1 個。
                                    */
                                    quantity: 1
                                })
                        }
                    );


                // ==============================
                // 5. HTTP Error
                // ==============================

                if (!response.ok) {

                    const message =
                        await getErrorMessage(
                            response
                        );

                    throw new Error(
                        message
                    );
                }


                // ==============================
                // 6. JSON Response
                // ==============================

                /*
                    Step 6 API 成功會回：

                    {
                        items: [...],
                        totalQuantity: 3,
                        totalAmount: 123.45
                    }
                */
                const cart =
                    await response.json();


                // ==============================
                // 7. 顯示成功
                // ==============================

                showFeedback(
                    feedback,
                    `已加入購物車，目前共 ${cart.totalQuantity} 件商品。`,
                    false
                );

            }
            catch (error) {

                /*
                    包含：

                    HTTP 400
                    HTTP 404
                    HTTP 500
                    network error
                    JSON error
                    ...
                */

                console.error(
                    "加入購物車失敗：",
                    error
                );


                showFeedback(
                    feedback,
                    error instanceof Error
                        ? error.message
                        : "加入購物車失敗。",
                    true
                );

            }
            finally {

                // ==============================
                // 8. 恢復 Button
                // ==============================

                button.disabled = false;

                button.textContent =
                    originalText;
            }
        }
    );
});


// ==========================================
// Feedback helper
// ==========================================

/*
    將成功 / 失敗訊息顯示到：

    <p class="cart-feedback">
*/
function showFeedback(
    element,
    message,
    isError
) {
    /*
        ?. 是 optional chaining。

        如果 element 是 null，
        就不要繼續操作。
    */
    if (!element) {
        return;
    }


    element.textContent =
        message;


    /*
        classList.toggle(
            className,
            condition
        )

        condition = true
        → 加入 class

        condition = false
        → 移除 class
    */
    element.classList.toggle(
        "cart-feedback--error",
        isError
    );


    element.classList.toggle(
        "cart-feedback--success",
        !isError && message !== ""
    );
}


// ==========================================
// API error helper
// ==========================================

/*
    不同錯誤 Response 格式可能不完全相同。

    例如 CartEndpoints：

    {
        "message": "商品庫存不足"
    }

    Validation：

    {
        "errors": {
            ...
        }
    }

    ProblemDetails：

    {
        "detail": "..."
    }

    所以集中在這裡處理。
*/
async function getErrorMessage(
    response
) {
    try {

        const data =
            await response.json();


        // CartEndpoints 自訂錯誤
        if (typeof data.message === "string") {
            return data.message;
        }


        // ProblemDetails
        if (typeof data.detail === "string") {
            return data.detail;
        }


        /*
            Minimal API Validation
            可能有 errors object。

            取第一個 validation error
            顯示給使用者。
        */
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

        /*
            如果 response body
            根本不是 JSON，
            就忽略 JSON parsing error。
        */
    }


    return `加入購物車失敗（HTTP ${response.status}）。`;
}
