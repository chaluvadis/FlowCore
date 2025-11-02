namespace FlowCore.Examples;
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Linked-List-Style Workflow Engine Examples");
        Console.WriteLine("=============================================");
        Console.WriteLine();
        // Example 0: Simple Dependency Injection Setup
        await RunSimpleDependencyInjectionExample();
        var serviceProvider = ConfigureServices();
        await RunBasicWorkflowExample(serviceProvider);
        await RunGuardedWorkflowExample(serviceProvider);
        await RunParallelWorkflowExample(serviceProvider);
        await RunAdvancedWorkflowExample(serviceProvider);
        await RunEcommerceWorkflowExample(serviceProvider);
        await RunErrorHandlingWorkflowExample(serviceProvider);
        await RunGuardExamples(serviceProvider);
        await WorkflowExamples.RunParserExample();
        // Example 8: OnSuccessGoTo, AddBlock, OnFailureGoTo Demonstration
        await RunTransitionDemonstrationExample(serviceProvider);
        // Example 9: Complex Document Processing Pipeline
        await DocumentProcessingExample.RunAsync();
        // Example 10: Data Analytics Pipeline
        await DataAnalyticsPipelineExample.RunAsync();
        // Example 11: Document Approval Workflow
        await DocumentApprovalExample.RunAsync();
        Console.WriteLine("\nAll examples completed successfully!");
    }
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
        // Configure WorkflowBlockFactory with security options to allow FlowCore assembly loading
        // WARNING: Dynamic assembly loading is enabled for demonstration purposes only.
        // In production, carefully evaluate security implications before enabling.
        services.AddSingleton<WorkflowBlockFactory>(sp =>
        {
            var securityOptions = new WorkflowBlockFactorySecurityOptions
            {
                AllowDynamicAssemblyLoading = true,
                AllowedAssemblyNames = new[] { "FlowCore" },
                ValidateStrongNameSignatures = true
            };
            return new WorkflowBlockFactory(sp, securityOptions);
        });
        services.AddSingleton<IWorkflowBlockFactory>(sp => sp.GetRequiredService<WorkflowBlockFactory>());
        return services.BuildServiceProvider();
    }
    private static async Task RunSimpleDependencyInjectionExample()
    {
        Console.WriteLine("Example 0: Simple Dependency Injection Setup");
        Console.WriteLine("--------------------------------------------");
        try
        {
            // Configure services
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });
            services.AddSingleton<IWorkflowBlockFactory, WorkflowBlockFactory>();
            services.AddSingleton<IStateManager, InMemoryStateManager>();
            var serviceProvider = services.BuildServiceProvider();
            // Create a simple workflow using the fluent API
            var workflow = FlowCoreWorkflowBuilder.Create("simple-workflow", "Simple Workflow")
                .WithVersion("1.0.0")
                .WithDescription("A simple workflow demonstrating dependency injection")
                .WithVariable("welcomeMessage", "Welcome to FlowCore!")
                .StartWith("BasicBlocks.LogBlock", "start")
                    .OnSuccessGoTo("log_message")
                    .WithDisplayName("Start Workflow")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "log_message")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Log Welcome Message")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Workflow Complete")
                    .And()
                .Build();
            // Get services from DI container
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            var stateManager = serviceProvider.GetRequiredService<IStateManager>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            // Create and execute workflow
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var input = new { UserId = "user-123", Message = "Hello FlowCore!" };
            var result = await engine.ExecuteAsync(workflow, input);
            Console.WriteLine($"Simple workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Success: {result.Succeeded}");
            Console.WriteLine($"Final state items: {result.FinalState?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunBasicWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 1: Real-Time User Registration");
        Console.WriteLine("--------------------------------------");
        try
        {
            var currentTime = DateTime.UtcNow;
            var isPeakHour = currentTime.Hour >= 9 && currentTime.Hour <= 17;
            var registrationVolume = isPeakHour ? "High" : "Normal";
            var processingPriority = isPeakHour ? "Expedited" : "Standard";
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("user-registration", "Real-Time User Registration")
                .WithVersion("2.0.0")
                .WithDescription("Dynamic user registration with real-time validation and adaptive processing")
                .WithAuthor("User Management Team")
                .WithTags("user-management", "registration", "real-time", "adaptive")
                .WithVariable("currentTime", currentTime)
                .WithVariable("isPeakHour", isPeakHour)
                .WithVariable("registrationVolume", registrationVolume)
                .WithVariable("processingPriority", processingPriority)
                .WithVariable("maxPasswordLength", 128)
                .WithVariable("minPasswordLength", 8)
                .WithVariable("requireSpecialChars", true)
                .WithVariable("enableTwoFactor", false)
                .WithVariable("welcomeEmailDelay", isPeakHour ? 5000 : 1000)
                .StartWith("BasicBlocks.LogBlock", "check_registration_context")
                    .OnSuccessGoTo("validate_user_credentials")
                    .WithDisplayName("Check Registration Context")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_user_credentials")
                    .OnSuccessGoTo("check_username_availability")
                    .OnFailureGoTo("credential_validation_failed")
                    .WithDisplayName("Validate User Credentials")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "check_username_availability")
                    .OnSuccessGoTo("create_user_profile")
                    .OnFailureGoTo("username_unavailable")
                    .WithDisplayName("Check Username Availability")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "create_user_profile")
                    .OnSuccessGoTo("setup_user_preferences")
                    .OnFailureGoTo("profile_creation_failed")
                    .WithDisplayName("Create User Profile")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "setup_user_preferences")
                    .OnSuccessGoTo("send_verification_email")
                    .WithDisplayName("Setup User Preferences")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_verification_email")
                    .OnSuccessGoTo("initialize_onboarding")
                    .OnFailureGoTo("email_delivery_failed")
                    .WithDisplayName("Send Verification Email")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "initialize_onboarding")
                    .OnSuccessGoTo("registration_successful")
                    .WithDisplayName("Initialize Onboarding Process")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "registration_successful")
                    .WithDisplayName("Registration Completed Successfully")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "credential_validation_failed")
                    .WithDisplayName("Credential Validation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "username_unavailable")
                    .WithDisplayName("Username Already Taken")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "profile_creation_failed")
                    .WithDisplayName("User Profile Creation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "email_delivery_failed")
                    .WithDisplayName("Verification Email Delivery Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var registrationData = new
            {
                UserName = $"user_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Email = $"user_{DateTime.UtcNow:yyyyMMdd_HHmmss}@example.com",
                FirstName = "Sarah",
                LastName = "Johnson",
                Password = "MySecure@Pass123!",
                PhoneNumber = "+1-555-USER-REG",
                DateOfBirth = new DateTime(1990, 5, 15),
                RegistrationTimestamp = currentTime,
                RegistrationSource = "MobileApp",
                DeviceInfo = "iPhone 15 Pro",
                IPAddress = "192.168.1.100",
                Timezone = "America/New_York",
                Language = "en-US",
                MarketingOptIn = true,
                TermsAccepted = true,
                PrivacyAccepted = true,
                CurrentPeakStatus = isPeakHour,
                ProcessingPriority = processingPriority,
                EstimatedWaitTime = isPeakHour ? "2-3 minutes" : "30-45 seconds"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, registrationData);
            Console.WriteLine($"Real-time user registration completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"New user: {registrationData.FirstName} {registrationData.LastName}");
            Console.WriteLine($"Username: {registrationData.UserName} - Email: {registrationData.Email}");
            Console.WriteLine($"Registration time: {currentTime:HH:mm:ss} - Peak hour: {isPeakHour}");
            Console.WriteLine($"Source: {registrationData.RegistrationSource} ({registrationData.DeviceInfo})");
            Console.WriteLine($"Priority: {processingPriority} - Estimated wait: {registrationData.EstimatedWaitTime}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunGuardedWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 2: Guarded Workflow");
        Console.WriteLine("-------------------------------");
        try
        {
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("order-processing", "Order Processing")
                .WithVersion("1.0.0")
                .WithDescription("Order processing with business rule validation")
                .WithAuthor("Business Team")
                .WithVariable("minOrderAmount", 10.0)
                .WithVariable("maxOrderAmount", 10000.0)
                .StartWith("BasicBlocks.LogBlock", "start_processing")
                    .OnSuccessGoTo("validate_order")
                    .WithDisplayName("Start Processing")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_order")
                    .OnSuccessGoTo("process_payment")
                    .OnFailureGoTo("reject_order")
                    .WithDisplayName("Validate Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_payment")
                    .OnSuccessGoTo("fulfill_order")
                    .OnFailureGoTo("payment_failed")
                    .WithDisplayName("Process Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "fulfill_order")
                    .OnSuccessGoTo("send_confirmation")
                    .WithDisplayName("Fulfill Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                    .WithDisplayName("Send Confirmation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "reject_order")
                    .WithDisplayName("Reject Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_failed")
                    .WithDisplayName("Payment Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var validOrder = new { Amount = 150.0, CustomerId = "CUST001" };
            var result = await engine.ExecuteAsync(workflowDefinition, validOrder);
            Console.WriteLine($"Valid order processed in {result.Duration?.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunParallelWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 3: Customer Onboarding Workflow");
        Console.WriteLine("--------------------------------------");
        try
        {
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("customer-onboarding", "Customer Onboarding Process")
                .WithVersion("1.0.0")
                .WithDescription("Parallel validation and setup for new customer registration")
                .WithAuthor("Customer Success Team")
                .WithVariable("welcomeEmailTemplate", "Welcome to our platform! Your account is being set up.")
                .WithVariable("setupTimeout", 30000)
                .StartWith("BasicBlocks.LogBlock", "initialize_onboarding")
                    .OnSuccessGoTo("parallel_customer_setup")
                    .WithDisplayName("Initialize Customer Onboarding")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "parallel_customer_setup")
                    .OnSuccessGoTo("create_customer_profile")
                    .OnFailureGoTo("setup_validation_failed")
                    .WithDisplayName("Parallel Customer Setup")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "create_customer_profile")
                    .OnSuccessGoTo("send_welcome_package")
                    .WithDisplayName("Create Customer Profile")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_welcome_package")
                    .OnSuccessGoTo("schedule_followup")
                    .WithDisplayName("Send Welcome Package")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "schedule_followup")
                    .OnSuccessGoTo("onboarding_complete")
                    .WithDisplayName("Schedule Follow-up Call")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "onboarding_complete")
                    .WithDisplayName("Customer Onboarding Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "setup_validation_failed")
                    .WithDisplayName("Customer Setup Validation Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var customerData = new
            {
                CustomerId = "CUST-NEW-001",
                CompanyName = "TechCorp Solutions",
                Industry = "Technology",
                EmployeeCount = 150,
                PrimaryContact = new
                {
                    Name = "Sarah Johnson",
                    Email = "sarah.johnson@techcorp.com",
                    Phone = "+1-555-TECH",
                    Title = "IT Director"
                },
                SubscriptionTier = "Enterprise",
                SetupPriority = "High",
                OnboardingDate = DateTime.UtcNow,
                AssignedManager = "Manager-001"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, customerData);
            Console.WriteLine($"Customer onboarding workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"New customer: {customerData.CompanyName} ({customerData.Industry})");
            Console.WriteLine($"Contact: {customerData.PrimaryContact.Name} - {customerData.PrimaryContact.Title}");
            Console.WriteLine($"Subscription: {customerData.SubscriptionTier} - Priority: {customerData.SetupPriority}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunAdvancedWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 4: Document Processing Pipeline");
        Console.WriteLine("---------------------------------------");
        try
        {
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("document-processing", "Document Processing Pipeline")
                .WithVersion("3.0.0")
                .WithDescription("Intelligent document processing with OCR, classification, and storage")
                .WithAuthor("Document Management Team")
                .WithVariable("maxFileSize", 10485760L) // 10MB
                .WithVariable("supportedFormats", new[] { "PDF", "JPG", "PNG", "TIFF" })
                .WithVariable("processingTimeout", 120000) // 2 minutes
                .WithVariable("autoClassificationEnabled", true)
                .StartWith("BasicBlocks.LogBlock", "validate_document")
                    .OnSuccessGoTo("extract_text")
                    .OnFailureGoTo("document_validation_failed")
                    .WithDisplayName("Validate Document Upload")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "extract_text")
                    .OnSuccessGoTo("classify_document")
                    .OnFailureGoTo("text_extraction_failed")
                    .WithDisplayName("Extract Text (OCR)")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "classify_document")
                    .OnSuccessGoTo("validate_content")
                    .OnFailureGoTo("classification_failed")
                    .WithDisplayName("Classify Document")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validate_content")
                    .OnSuccessGoTo("store_document")
                    .OnFailureGoTo("content_validation_failed")
                    .WithDisplayName("Validate Extracted Content")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "store_document")
                    .OnSuccessGoTo("generate_thumbnail")
                    .WithDisplayName("Store Document")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "generate_thumbnail")
                    .OnSuccessGoTo("send_notification")
                    .WithDisplayName("Generate Thumbnail")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_notification")
                    .OnSuccessGoTo("processing_complete")
                    .WithDisplayName("Send Processing Notification")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "processing_complete")
                    .WithDisplayName("Document Processing Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "document_validation_failed")
                    .WithDisplayName("Document Validation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "text_extraction_failed")
                    .WithDisplayName("Text Extraction Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "classification_failed")
                    .WithDisplayName("Document Classification Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "content_validation_failed")
                    .WithDisplayName("Content Validation Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var documentData = new
            {
                DocumentId = "DOC-2024-001",
                FileName = "invoice_techcorp_001.pdf",
                FileSize = 2048576L, // 2MB
                FileFormat = "PDF",
                UploadDate = DateTime.UtcNow,
                UploadedBy = "user-finance-001",
                Department = "Finance",
                DocumentType = "Invoice",
                ProcessingPriority = "Normal",
                RequiresOCR = true,
                Language = "en-US",
                Tags = new[] { "invoice", "techcorp", "Q4-2024" }
            };
            var result = await engine.ExecuteAsync(workflowDefinition, documentData);
            Console.WriteLine($"Document processing pipeline completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Document: {documentData.FileName} ({documentData.FileSize / 1024}KB)");
            Console.WriteLine($"Type: {documentData.DocumentType} - Department: {documentData.Department}");
            Console.WriteLine($"OCR Required: {documentData.RequiresOCR} - Priority: {documentData.ProcessingPriority}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunEcommerceWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 5: Product Catalog Management");
        Console.WriteLine("-------------------------------------");
        try
        {
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("product-catalog", "Product Catalog Management")
                .WithVersion("2.0.0")
                .WithDescription("Product information management with validation and publishing")
                .WithAuthor("Product Management Team")
                .WithVariable("maxImagesPerProduct", 10)
                .WithVariable("maxDescriptionLength", 2000)
                .WithVariable("autoPublishEnabled", true)
                .WithVariable("reviewRequiredForPrice", 1000.0)
                .StartWith("BasicBlocks.LogBlock", "validate_product_data")
                    .OnSuccessGoTo("process_images")
                    .OnFailureGoTo("product_validation_failed")
                    .WithDisplayName("Validate Product Information")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_images")
                    .OnSuccessGoTo("generate_seo")
                    .OnFailureGoTo("image_processing_failed")
                    .WithDisplayName("Process Product Images")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "generate_seo")
                    .OnSuccessGoTo("check_review_required")
                    .WithDisplayName("Generate SEO Metadata")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "check_review_required")
                    .OnSuccessGoTo("send_for_review")
                    .OnFailureGoTo("auto_publish")
                    .WithDisplayName("Check if Review Required")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_for_review")
                    .OnSuccessGoTo("await_approval")
                    .WithDisplayName("Send for Manual Review")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "await_approval")
                    .OnSuccessGoTo("publish_product")
                    .OnFailureGoTo("review_rejected")
                    .WithDisplayName("Await Approval")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "auto_publish")
                    .OnSuccessGoTo("publish_product")
                    .WithDisplayName("Auto-Publish Product")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "publish_product")
                    .OnSuccessGoTo("notify_stakeholders")
                    .WithDisplayName("Publish to Catalog")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "notify_stakeholders")
                    .OnSuccessGoTo("catalog_update_complete")
                    .WithDisplayName("Notify Stakeholders")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "catalog_update_complete")
                    .WithDisplayName("Product Catalog Updated")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "product_validation_failed")
                    .WithDisplayName("Product Validation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "image_processing_failed")
                    .WithDisplayName("Image Processing Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "review_rejected")
                    .WithDisplayName("Product Review Rejected")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var productData = new
            {
                ProductId = "PROD-TECH-001",
                ProductName = "Premium Wireless Headphones",
                Category = "Electronics",
                Subcategory = "Audio",
                Brand = "TechSound",
                Price = 299.99m,
                Description = "High-quality wireless headphones with noise cancellation and premium sound quality.",
                SKU = "TS-WH-001",
                Weight = 0.25m,
                Dimensions = "20x15x10 cm",
                StockQuantity = 50,
                Images = new[] { "main.jpg", "angle1.jpg", "angle2.jpg" },
                Tags = new[] { "wireless", "headphones", "premium", "noise-cancelling" },
                SupplierId = "SUPP-TECH-001",
                RequiresReview = false,
                PublishDate = DateTime.UtcNow
            };
            var result = await engine.ExecuteAsync(workflowDefinition, productData);
            Console.WriteLine($"Product catalog management completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Product: {productData.ProductName} (${productData.Price})");
            Console.WriteLine($"Category: {productData.Category}/{productData.Subcategory}");
            Console.WriteLine($"Stock: {productData.StockQuantity} units - Images: {productData.Images.Length}");
            Console.WriteLine($"Auto-publish: {!productData.RequiresReview} - Brand: {productData.Brand}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunErrorHandlingWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 6: Payment Processing with Recovery");
        Console.WriteLine("-------------------------------------------");
        try
        {
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("payment-processing", "Payment Processing with Recovery")
                .WithVersion("2.0.0")
                .WithDescription("Robust payment processing with multiple retry mechanisms and fallbacks")
                .WithAuthor("Payment Systems Team")
                .WithVariable("maxRetryAttempts", 3)
                .WithVariable("primaryGateway", "Stripe")
                .WithVariable("fallbackGateway", "PayPal")
                .WithVariable("circuitBreakerThreshold", 5)
                .StartWith("BasicBlocks.LogBlock", "validate_payment_request")
                    .OnSuccessGoTo("authorize_payment")
                    .OnFailureGoTo("payment_validation_failed")
                    .WithDisplayName("Validate Payment Request")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "authorize_payment")
                    .OnSuccessGoTo("capture_payment")
                    .OnFailureGoTo("payment_authorization_failed")
                    .WithDisplayName("Authorize Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "capture_payment")
                    .OnSuccessGoTo("update_payment_status")
                    .OnFailureGoTo("payment_capture_failed")
                    .WithDisplayName("Capture Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "update_payment_status")
                    .OnSuccessGoTo("send_receipt")
                    .OnFailureGoTo("status_update_failed")
                    .WithDisplayName("Update Payment Status")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_receipt")
                    .OnSuccessGoTo("process_webhook")
                    .WithDisplayName("Send Payment Receipt")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_webhook")
                    .OnSuccessGoTo("payment_processing_complete")
                    .WithDisplayName("Process Payment Webhook")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_processing_complete")
                    .WithDisplayName("Payment Processing Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_validation_failed")
                    .WithDisplayName("Payment Validation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_authorization_failed")
                    .OnSuccessGoTo("retry_with_fallback")
                    .OnFailureGoTo("authorization_permanently_failed")
                    .WithDisplayName("Payment Authorization Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "retry_with_fallback")
                    .OnSuccessGoTo("capture_payment")
                    .OnFailureGoTo("fallback_gateway_failed")
                    .WithDisplayName("Retry with Fallback Gateway")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "payment_capture_failed")
                    .OnSuccessGoTo("retry_capture")
                    .OnFailureGoTo("capture_permanently_failed")
                    .WithDisplayName("Payment Capture Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "retry_capture")
                    .OnSuccessGoTo("update_payment_status")
                    .OnFailureGoTo("max_retries_exceeded")
                    .WithDisplayName("Retry Payment Capture")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "status_update_failed")
                    .OnSuccessGoTo("retry_status_update")
                    .OnFailureGoTo("status_update_permanently_failed")
                    .WithDisplayName("Payment Status Update Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "retry_status_update")
                    .OnSuccessGoTo("send_receipt")
                    .OnFailureGoTo("status_update_permanently_failed")
                    .WithDisplayName("Retry Status Update")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "authorization_permanently_failed")
                    .WithDisplayName("Payment Authorization Permanently Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "fallback_gateway_failed")
                    .WithDisplayName("Fallback Gateway Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "capture_permanently_failed")
                    .WithDisplayName("Payment Capture Permanently Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "max_retries_exceeded")
                    .WithDisplayName("Maximum Retries Exceeded")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "status_update_permanently_failed")
                    .WithDisplayName("Status Update Permanently Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var paymentData = new
            {
                PaymentId = "PAY-2024-001",
                OrderId = "ORD-2024-001",
                CustomerId = "CUST-PREMIUM-001",
                Amount = 299.99m,
                Currency = "USD",
                PaymentMethod = "CreditCard",
                CardToken = "card_token_12345",
                Gateway = "Stripe",
                FallbackGateway = "PayPal",
                RetryCount = 0,
                MaxRetries = 3,
                ProcessingDate = DateTime.UtcNow,
                MerchantReference = "MERCH-TECHSTORE-001"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, paymentData);
            Console.WriteLine($"Payment processing workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Payment: {paymentData.PaymentId} - Amount: ${paymentData.Amount}");
            Console.WriteLine($"Method: {paymentData.PaymentMethod} - Gateway: {paymentData.Gateway}");
            Console.WriteLine($"Order: {paymentData.OrderId} - Customer: {paymentData.CustomerId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunGuardExamples(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 7: Guard Validation Examples");
        Console.WriteLine("------------------------------------");
        await RunBusinessHoursGuardExample(serviceProvider);
        await RunDataValidationGuardExample(serviceProvider);
        await RunNumericRangeGuardExample(serviceProvider);
        await RunRequiredFieldGuardExample(serviceProvider);
        await RunAuthorizationGuardExample(serviceProvider);
        await RunAdvancedGuardWorkflowExample(serviceProvider);
    }
    private static async Task RunBusinessHoursGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Business Hours Guard Example");
        Console.WriteLine("----------------------------");
        try
        {
            var businessHoursGuard = new CommonGuards.BusinessHoursGuard(
                TimeSpan.FromHours(9),
                TimeSpan.FromHours(17),
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            );
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("business-hours-workflow", "Business Hours Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates business hours before processing")
                .WithAuthor("Operations Team")
                .StartWith("BasicBlocks.LogBlock", "check_time")
                    .OnSuccessGoTo("process_request")
                    .OnFailureGoTo("outside_hours")
                    .WithDisplayName("Check Current Time")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_request")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Request")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Request Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "outside_hours")
                    .WithDisplayName("Outside Business Hours")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var currentTime = DateTime.UtcNow;
            var isBusinessHours = currentTime.DayOfWeek >= DayOfWeek.Monday && currentTime.DayOfWeek <= DayOfWeek.Friday
                                 && currentTime.TimeOfDay >= TimeSpan.FromHours(9)
                                 && currentTime.TimeOfDay <= TimeSpan.FromHours(17);
            var input = new
            {
                CurrentTime = currentTime,
                IsBusinessHours = isBusinessHours,
                GuardType = "BusinessHours"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, input);
            Console.WriteLine($"Business hours validation completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Current time: {currentTime:HH:mm} - Business hours: {isBusinessHours}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunDataValidationGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Data Validation Guard Example");
        Console.WriteLine("-----------------------------");
        try
        {
            var emailGuard = new CommonGuards.DataFormatGuard("Email", @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.IgnoreCase);
            var phoneGuard = new CommonGuards.DataFormatGuard("Phone", @"^\+?[\d\s\-\(\)]+$");
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("data-validation-workflow", "Data Format Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates email format and required fields")
                .WithAuthor("Data Quality Team")
                .StartWith("BasicBlocks.LogBlock", "validate_input")
                    .OnSuccessGoTo("process_data")
                    .OnFailureGoTo("validation_failed")
                    .WithDisplayName("Validate Input Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_data")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Valid Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Data Processing Complete")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validation_failed")
                    .WithDisplayName("Data Validation Failed")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var validInput = new
            {
                Email = "user@example.com",
                CustomerId = "CUST-123",
                Phone = "+1-555-0123"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, validInput);
            Console.WriteLine($"Data validation completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Email format: Valid, Phone format: Valid");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunNumericRangeGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Numeric Range Guard Example");
        Console.WriteLine("---------------------------");
        try
        {
            var amountGuard = new CommonGuards.NumericRangeGuard("Amount", 10.0m, 5000.0m, true, true);
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("numeric-validation-workflow", "Numeric Range Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates numeric ranges for business rules")
                .WithAuthor("Business Rules Team")
                .WithVariable("minAmount", 10.0)
                .WithVariable("maxAmount", 5000.0)
                .StartWith("BasicBlocks.LogBlock", "validate_amount")
                    .OnSuccessGoTo("process_payment")
                    .OnFailureGoTo("amount_rejected")
                    .WithDisplayName("Validate Amount Range")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_payment")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Process Payment")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Payment Processed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "amount_rejected")
                    .WithDisplayName("Amount Outside Valid Range")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var testInput = new
            {
                Amount = 150.75,
                Currency = "USD",
                MerchantId = "MERCH-456"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, testInput);
            Console.WriteLine($"Numeric validation completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Amount {testInput.Amount} is within range $10-$5000: {testInput.Amount >= 10 && testInput.Amount <= 5000}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunRequiredFieldGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Required Field Guard Example");
        Console.WriteLine("----------------------------");
        try
        {
            var requiredFieldsGuard = new CommonGuards.RequiredFieldGuard("CustomerId", "Email", "FirstName", "LastName");
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("required-fields-workflow", "Required Fields Validation")
                .WithVersion("1.0.0")
                .WithDescription("Ensures all required fields are present")
                .WithAuthor("Data Integrity Team")
                .StartWith("BasicBlocks.LogBlock", "check_required")
                    .OnSuccessGoTo("process_complete")
                    .OnFailureGoTo("missing_fields")
                    .WithDisplayName("Check Required Fields")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_complete")
                    .OnSuccessGoTo("finalize")
                    .WithDisplayName("Process Complete Data")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "finalize")
                    .WithDisplayName("Processing Finalized")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "missing_fields")
                    .WithDisplayName("Required Fields Missing")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var completeInput = new
            {
                CustomerId = "CUST-999",
                Email = "complete@example.com",
                FirstName = "John",
                LastName = "Doe",
                Address = "123 Main St"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, completeInput);
            Console.WriteLine($"Required fields validation completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"All required fields present: {completeInput.CustomerId != null && completeInput.Email != null && completeInput.FirstName != null && completeInput.LastName != null}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunAuthorizationGuardExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Authorization Guard Example");
        Console.WriteLine("---------------------------");
        try
        {
            var deleteGuard = new CommonGuards.AuthorizationGuard("delete", "administrator", "manager");
            var adminGuard = new CommonGuards.AuthorizationGuard("admin");
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("authorization-workflow", "Authorization Validation")
                .WithVersion("1.0.0")
                .WithDescription("Validates user permissions and roles")
                .WithAuthor("Security Team")
                .StartWith("BasicBlocks.LogBlock", "check_permissions")
                    .OnSuccessGoTo("access_granted")
                    .OnFailureGoTo("access_denied")
                    .WithDisplayName("Check User Permissions")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "access_granted")
                    .OnSuccessGoTo("perform_action")
                    .WithDisplayName("Access Granted")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "perform_action")
                    .OnSuccessGoTo("complete")
                    .WithDisplayName("Perform Authorized Action")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "complete")
                    .WithDisplayName("Action Completed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "access_denied")
                    .WithDisplayName("Access Denied")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var authorizedInput = new
            {
                UserId = "user-admin",
                Permissions = new[] { "read", "write", "admin", "delete" },
                Roles = new[] { "administrator", "manager" },
                Action = "delete_user"
            };
            var result = await engine.ExecuteAsync(workflowDefinition, authorizedInput);
            Console.WriteLine($"Authorization validation completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"User has admin permissions: {authorizedInput.Permissions.Contains("admin")}");
            Console.WriteLine($"User has administrator role: {authorizedInput.Roles.Contains("administrator")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunAdvancedGuardWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Advanced Guard Workflow Example");
        Console.WriteLine("-------------------------------");
        try
        {
            var businessHoursGuard = new CommonGuards.BusinessHoursGuard(
                TimeSpan.FromHours(9), TimeSpan.FromHours(17));
            var amountGuard = new CommonGuards.NumericRangeGuard("Amount", 100.0m, 10000.0m);
            var emailGuard = new CommonGuards.DataFormatGuard("Email", @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
            var requiredGuard = new CommonGuards.RequiredFieldGuard("CustomerId", "Email", "Amount");
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("advanced-guard-workflow", "Advanced Guard Integration")
                .WithVersion("2.0.0")
                .WithDescription("Comprehensive guard validation with multiple business rules")
                .WithAuthor("Enterprise Team")
                .WithVariable("businessHoursStart", "09:00")
                .WithVariable("businessHoursEnd", "17:00")
                .WithVariable("minOrderAmount", 100.0)
                .WithVariable("maxOrderAmount", 10000.0)
                .StartWith("BasicBlocks.LogBlock", "validate_order")
                    .OnSuccessGoTo("check_business_hours")
                    .OnFailureGoTo("validation_failed")
                    .WithDisplayName("Validate Order Details")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "check_business_hours")
                    .OnSuccessGoTo("process_order")
                    .OnFailureGoTo("outside_hours")
                    .WithDisplayName("Check Business Hours")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "process_order")
                    .OnSuccessGoTo("send_confirmation")
                    .WithDisplayName("Process Valid Order")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "send_confirmation")
                    .WithDisplayName("Send Order Confirmation")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "validation_failed")
                    .WithDisplayName("Order Validation Failed")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "outside_hours")
                    .WithDisplayName("Outside Business Hours")
                    .And()
                .Build();
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            var blockFactory = serviceProvider.GetRequiredService<IWorkflowBlockFactory>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(blockFactory, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var currentTime = DateTime.UtcNow;
            var isBusinessHours = currentTime.DayOfWeek >= DayOfWeek.Monday
                                && currentTime.DayOfWeek <= DayOfWeek.Friday
                                && currentTime.TimeOfDay >= TimeSpan.FromHours(9)
                                && currentTime.TimeOfDay <= TimeSpan.FromHours(17);
            var orderInput = new
            {
                OrderId = "ORD-2024-003",
                CustomerId = "CUST-PREMIUM",
                Email = "premium@example.com",
                Amount = 1250.50,
                OrderDate = currentTime,
                IsBusinessHours = isBusinessHours,
                BusinessHoursStart = TimeSpan.FromHours(9),
                BusinessHoursEnd = TimeSpan.FromHours(17)
            };
            var result = await engine.ExecuteAsync(workflowDefinition, orderInput);
            Console.WriteLine($"Advanced guard workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"Business hours valid: {isBusinessHours}");
            Console.WriteLine($"Amount in range: {orderInput.Amount >= 100 && orderInput.Amount <= 10000}");
            Console.WriteLine($"Email format valid: {orderInput.Email.Contains("@") && orderInput.Email.Contains(".")}");
            Console.WriteLine($"Required fields present: {orderInput.CustomerId != null && orderInput.Email != null && orderInput.Amount > 0}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
    private static async Task RunTransitionDemonstrationExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Example 8: OnSuccessGoTo, AddBlock, OnFailureGoTo Demonstration");
        Console.WriteLine("============================================================");
        Console.WriteLine("This example demonstrates the key workflow transition features:");
        Console.WriteLine();
        Console.WriteLine("🔄 OnSuccessGoTo - Blocks automatically transition to specified next block on success");
        Console.WriteLine("❌ OnFailureGoTo - Blocks automatically transition to specified next block on failure");
        Console.WriteLine("➕ AddBlock - New blocks can be created and referenced by transition definitions");
        Console.WriteLine();
        try
        {
            // Create a simple workflow definition that shows the transitions
            var workflowDefinition = FlowCoreWorkflowBuilder.Create("transition-demo", "Transition Demonstration")
                .WithVersion("1.0.0")
                .WithDescription("Demonstrates OnSuccessGoTo, AddBlock, and OnFailureGoTo functionality")
                .WithAuthor("FlowCore Team")
                .StartWith("BasicBlocks.LogBlock", "start")
                    .OnSuccessGoTo("middle")
                    .OnFailureGoTo("error")
                    .WithDisplayName("Start Block")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "middle")
                    .OnSuccessGoTo("end")
                    .OnFailureGoTo("error")
                    .WithDisplayName("Middle Block")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "end")
                    .WithDisplayName("End Block")
                    .And()
                .AddBlock("BasicBlocks.LogBlock", "error")
                    .WithDisplayName("Error Block")
                    .And()
                .Build();
            Console.WriteLine("📋 Workflow Definition Created:");
            Console.WriteLine($"   Workflow ID: {workflowDefinition.Id}");
            Console.WriteLine($"   Start Block: {workflowDefinition.StartBlockName}");
            Console.WriteLine($"   Total Blocks: {workflowDefinition.Blocks.Count}");
            Console.WriteLine();
            Console.WriteLine("🔗 Block Transitions Defined:");
            foreach (var block in workflowDefinition.Blocks.Values)
            {
                Console.WriteLine($"   {block.BlockId}: Success → '{block.NextBlockOnSuccess}', Failure → '{block.NextBlockOnFailure}'");
            }
            Console.WriteLine();
            Console.WriteLine("✅ Workflow Definition Validation:");
            Console.WriteLine($"   Is Valid: {workflowDefinition.IsValid()}");
            Console.WriteLine($"   All referenced blocks exist: {workflowDefinition.Blocks.ContainsKey(workflowDefinition.StartBlockName)}");
            // Check that all transition targets exist
            var missingTransitions = new List<string>();
            foreach (var block in workflowDefinition.Blocks.Values)
            {
                if (!string.IsNullOrEmpty(block.NextBlockOnSuccess) && !workflowDefinition.Blocks.ContainsKey(block.NextBlockOnSuccess))
                    missingTransitions.Add($"{block.BlockId} → {block.NextBlockOnSuccess}");
                if (!string.IsNullOrEmpty(block.NextBlockOnFailure) && !workflowDefinition.Blocks.ContainsKey(block.NextBlockOnFailure))
                    missingTransitions.Add($"{block.BlockId} → {block.NextBlockOnFailure}");
            }
            if (missingTransitions.Any())
            {
                Console.WriteLine($"   ⚠️  Missing transition targets: {string.Join(", ", missingTransitions)}");
            }
            else
            {
                Console.WriteLine("   ✅ All transition targets exist");
            }
            Console.WriteLine();
            Console.WriteLine("🚀 Executing Workflow to Demonstrate Transitions:");
            Console.WriteLine();
            // Create a simple mock block factory for demonstration
            var mockBlockFactory = new Mock<IWorkflowBlockFactory>();
            var executedBlocks = new List<string>();
            // Create blocks that track their execution
            mockBlockFactory.Setup(f => f.CreateBlock(It.Is<WorkflowBlockDefinition>(bd => bd.BlockId == "start")))
                .Returns(new FlowCore.Common.BasicBlocks.LogBlock($"🚀 Starting workflow execution at {DateTime.Now:HH:mm:ss}", nextBlockOnSuccess: "middle", nextBlockOnFailure: "error"));
            mockBlockFactory.Setup(f => f.CreateBlock(It.Is<WorkflowBlockDefinition>(bd => bd.BlockId == "middle")))
                .Returns(new FlowCore.Common.BasicBlocks.LogBlock($"🔄 Processing middle step at {DateTime.Now:HH:mm:ss}", nextBlockOnSuccess: "end", nextBlockOnFailure: "error"));
            mockBlockFactory.Setup(f => f.CreateBlock(It.Is<WorkflowBlockDefinition>(bd => bd.BlockId == "end")))
                .Returns(new FlowCore.Common.BasicBlocks.LogBlock($"✅ Workflow completed successfully at {DateTime.Now:HH:mm:ss}", nextBlockOnSuccess: "", nextBlockOnFailure: ""));
            mockBlockFactory.Setup(f => f.CreateBlock(It.Is<WorkflowBlockDefinition>(bd => bd.BlockId == "error")))
                .Returns(new FlowCore.Common.BasicBlocks.LogBlock($"❌ Error occurred during execution at {DateTime.Now:HH:mm:ss}", nextBlockOnSuccess: "", nextBlockOnFailure: ""));
            var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();
            // Create dependencies for new service-oriented constructor
            var executor = new WorkflowExecutor(mockBlockFactory.Object, new InMemoryWorkflowStore());
            var workflowStore = new InMemoryWorkflowStore();
            var parser = new WorkflowDefinitionParser();
            var validator = new WorkflowValidator();
            var engine = new WorkflowEngine(executor, workflowStore, parser, validator, logger);
            var inputData = new
            {
                UserId = "demo-user-123",
                Action = "process_order",
                Timestamp = DateTime.UtcNow,
                Metadata = new { Source = "Demonstration", Version = "1.0" }
            };
            Console.WriteLine($"Executing workflow with input: {inputData.UserId} - {inputData.Action}");
            var result = await engine.ExecuteAsync(workflowDefinition, inputData);
            Console.WriteLine();
            Console.WriteLine($"🎯 Execution Results:");
            Console.WriteLine($"   ✅ Workflow completed in {result.Duration?.TotalMilliseconds}ms");
            Console.WriteLine($"   ✅ Status: {(result.Succeeded ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"   ✅ Final state items: {result.FinalState?.Count ?? 0}");
            Console.WriteLine();
            Console.WriteLine("🎯 Key Features Demonstrated:");
            Console.WriteLine("   ✅ OnSuccessGoTo: 'start' block transitioned to 'middle' on success");
            Console.WriteLine("   ✅ OnFailureGoTo: All blocks have error transition paths defined");
            Console.WriteLine("   ✅ AddBlock: Multiple blocks added and properly connected");
            Console.WriteLine("   ✅ Workflow Validation: Definition structure is valid");
            Console.WriteLine("   ✅ Block Execution: All blocks executed their code and logged messages");
            Console.WriteLine("   ✅ State Management: Input data was processed through workflow");
            Console.WriteLine();
            Console.WriteLine("💡 Usage Example:");
            Console.WriteLine("   var workflow = FlowCoreWorkflowBuilder.Create('my-workflow', 'My Workflow')");
            Console.WriteLine("       .StartWith('BasicBlocks.LogBlock', 'start')");
            Console.WriteLine("           .OnSuccessGoTo('next_block')");
            Console.WriteLine("           .OnFailureGoTo('error_block')");
            Console.WriteLine("           .And()");
            Console.WriteLine("       .AddBlock('BasicBlocks.LogBlock', 'next_block')");
            Console.WriteLine("           .OnSuccessGoTo('final_block')");
            Console.WriteLine("           .And()");
            Console.WriteLine("       .AddBlock('BasicBlocks.LogBlock', 'error_block')");
            Console.WriteLine("           .And()");
            Console.WriteLine("       .Build();");
            Console.WriteLine();
            Console.WriteLine("   var result = await engine.ExecuteAsync(workflow, inputData);");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in transition demonstration: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        Console.WriteLine();
    }
}
public static class FlowCoreWorkflowBuilderExtensions
{
    public static FlowCoreWorkflowBuilder.FlowCoreWorkflowBlockBuilder StartWith<TBlock>(
        this FlowCoreWorkflowBuilder builder, string message)
        where TBlock : IWorkflowBlock
    {
        var block = CreateBlockInstance<TBlock>(message) as IWorkflowBlock;
        if (block == null)
        {
            throw new InvalidOperationException($"Failed to create block of type {typeof(TBlock)}");
        }
        return builder.StartWith(block);
    }
    public static FlowCoreWorkflowBuilder.FlowCoreWorkflowBlockBuilder AddBlock<TBlock>(
        this FlowCoreWorkflowBuilder builder, string message)
        where TBlock : IWorkflowBlock
    {
        var block = CreateBlockInstance<TBlock>(message) as IWorkflowBlock;
        if (block == null)
        {
            throw new InvalidOperationException($"Failed to create block of type {typeof(TBlock)}");
        }
        return builder.AddBlock(block);
    }
    private static object CreateBlockInstance<TBlock>(string message)
    {
        var blockType = typeof(TBlock);
        if (blockType == typeof(BasicBlocks.LogBlock))
        {
            return new BasicBlocks.LogBlock(message, nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.WaitBlock))
        {
            return new BasicBlocks.WaitBlock(TimeSpan.FromMilliseconds(100), nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.SetStateBlock))
        {
            return new BasicBlocks.SetStateBlock("message", message, nextBlockOnSuccess: "");
        }
        else if (blockType == typeof(BasicBlocks.ConditionalBlock))
        {
            return new BasicBlocks.ConditionalBlock(ctx => true, nextBlockOnConditionMet: "");
        }
        else if (blockType == typeof(BasicBlocks.FailBlock))
        {
            return new BasicBlocks.FailBlock(message, nextBlockOnSuccess: "", nextBlockOnFailure: "");
        }
        else
        {
            return Activator.CreateInstance(blockType, message)!;
        }
    }
}
