using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TaskManager.Api.Controllers;
using TaskManager.Api.DTOs;
using TaskManager.Api.Hubs;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests;

public class TasksControllerTests
{
    private readonly Mock<ITaskService> _mockTaskService;
    private readonly Mock<IHubContext<TaskHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockHubClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly TasksController _controller;
    private const string TestUserId = "user-123";

    public TasksControllerTests()
    {
        // Initialize our dependency mocks
        _mockTaskService = new Mock<ITaskService>();
        _mockHubContext = new Mock<IHubContext<TaskHub>>();
        _mockHubClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        // Wire up SignalR mocks so _hubContext.Clients.All.SendAsync doesn't throw a NullReferenceException
        _mockHubClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        _mockHubContext.Setup(c => c.Clients).Returns(_mockHubClients.Object);

        // Instantiate our system under test (SUT)
        _controller = new TasksController(_mockTaskService.Object, _mockHubContext.Object);

        // Mock the User context (ClaimsPrincipal) so CurrentUserId functions properly
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId)
        }, "mock-auth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };
    }

    // ==========================================
    // 1. GET: api/tasks (GetTasks)
    // ==========================================
    [Fact]
    public async Task GetTasks_ReturnsOkResult_WithListOfTasks()
    {
        // Arrange
        var mockTasks = new List<TaskResponseDto>
        {
            new(1, "Task One", "Desc One", false, DateTime.UtcNow),
            new(2, "Task Two", "Desc Two", true, DateTime.UtcNow)
        };

        _mockTaskService
            .Setup(s => s.GetTasksAsync(TestUserId))
            .ReturnsAsync(mockTasks);

        // Act
        var result = await _controller.GetTasks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTasks = Assert.IsType<List<TaskResponseDto>>(okResult.Value);
        Assert.Equal(2, returnedTasks.Count);
    }

    // ==========================================
    // 2. GET: api/tasks/{id} (GetTask)
    // ==========================================
    [Fact]
    public async Task GetTask_ReturnsOkResult_WhenTaskExists()
    {
        // Arrange
        int taskId = 1;
        var mockTask = new TaskResponseDto(taskId, "Target Task", "Desc", false, DateTime.UtcNow);

        _mockTaskService
            .Setup(s => s.GetTaskByIdAsync(taskId, TestUserId))
            .ReturnsAsync(mockTask);

        // Act
        var result = await _controller.GetTask(taskId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTask = Assert.IsType<TaskResponseDto>(okResult.Value);
        Assert.Equal(taskId, returnedTask.Id);
    }

    [Fact]
    public async Task GetTask_ReturnsNotFound_WhenTaskDoesNotExist()
    {
        // Arrange
        int missingId = 99;
        _mockTaskService
            .Setup(s => s.GetTaskByIdAsync(missingId, TestUserId))
            .ReturnsAsync((TaskResponseDto?)null);

        // Act
        var result = await _controller.GetTask(missingId);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ==========================================
    // 3. POST: api/tasks (CreateTask)
    // ==========================================
    [Fact]
    public async Task CreateTask_ReturnsCreatedResult_AndBroadcastsSignalR()
    {
        // Arrange
        var inputDto = new CreateTaskDto("New Task", "New Desc");
        var outputDto = new TaskResponseDto(10, "New Task", "New Desc", false, DateTime.UtcNow);

        _mockTaskService
            .Setup(s => s.CreateTaskAsync(inputDto, TestUserId))
            .ReturnsAsync(outputDto);

        // Act
        var result = await _controller.CreateTask(inputDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedTask = Assert.IsType<TaskResponseDto>(createdResult.Value);
        Assert.Equal(10, returnedTask.Id);
        Assert.Equal("GetTask", createdResult.ActionName);

        // SignalR Verification: Ensure SendAsync was actually invoked with the correct event name and payload
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "TaskCreated", 
                It.Is<object[]>(args => args.Length == 1 && args[0] == outputDto), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    // ==========================================
    // 4. PUT: api/tasks/{id} (UpdateTask)
    // ==========================================
    [Fact]
    public async Task UpdateTask_ReturnsNoContent_AndBroadcastsSignalR_WhenSuccessful()
    {
        // Arrange
        int taskId = 5;
        var inputDto = new UpdateTaskDto("Updated Title", "Updated Desc", true);
        var refreshedDto = new TaskResponseDto(taskId, "Updated Title", "Updated Desc", true, DateTime.UtcNow);

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync(taskId, inputDto, TestUserId))
            .ReturnsAsync(true);

        _mockTaskService
            .Setup(s => s.GetTaskByIdAsync(taskId, TestUserId))
            .ReturnsAsync(refreshedDto);

        // Act
        var result = await _controller.UpdateTask(taskId, inputDto);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // SignalR Verification: Ensure the updated properties broadcast down the line
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "TaskUpdated", 
                It.Is<object[]>(args => args.Length == 1 && args[0] == refreshedDto), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateTask_ReturnsNotFound_WhenTaskToUpdateDoesNotExist()
    {
        // Arrange
        int missingId = 404;
        var inputDto = new UpdateTaskDto("Title", "Desc", true);

        _mockTaskService
            .Setup(s => s.UpdateTaskAsync(missingId, inputDto, TestUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateTask(missingId, inputDto);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        // Ensure no messages are sent over the websocket when failing
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    // ==========================================
    // 5. DELETE: api/tasks/{id} (DeleteTask)
    // ==========================================
    [Fact]
    public async Task DeleteTask_ReturnsNoContent_AndBroadcastsSignalRId_WhenSuccessful()
    {
        // Arrange
        int targetId = 7;
        _mockTaskService
            .Setup(s => s.DeleteTaskAsync(targetId, TestUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTask(targetId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // SignalR Verification: Ensure the precise ID is broadcasted so the UI knows which item to evict
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "TaskDeleted", 
                It.Is<object[]>(args => args.Length == 1 && (int)args[0] == targetId), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task DeleteTask_ReturnsNotFound_WhenTaskToDeleteDoesNotExist()
    {
        // Arrange
        int missingId = 500;
        _mockTaskService
            .Setup(s => s.DeleteTaskAsync(missingId, TestUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTask(missingId);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        // Ensure WebSocket is bypassed
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }
}
