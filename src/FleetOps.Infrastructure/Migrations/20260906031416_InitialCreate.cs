using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    license_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    processed_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    mileage = table.Column<int>(type: "integer", nullable: false),
                    capacity_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    current_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicles_drivers_current_driver_id",
                        column: x => x.current_driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    origin_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    origin_neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    origin_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    destination_neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination_country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    assigned_vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estimated_delivery_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_delivery_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_deliveries_drivers_assigned_driver_id",
                        column: x => x.assigned_driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliveries_vehicles_assigned_vehicle_id",
                        column: x => x.assigned_vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cost_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cost_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenances", x => x.id);
                    table.ForeignKey(
                        name: "FK_maintenances_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    origin_neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    origin_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin_country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    destination_neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination_country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    planned_departure = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actual_departure = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estimated_arrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actual_arrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivery_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.id);
                    table.ForeignKey(
                        name: "FK_routes_drivers_assigned_driver_id",
                        column: x => x.assigned_driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_routes_vehicles_assigned_vehicle_id",
                        column: x => x.assigned_vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_assigned_driver_id",
                table: "deliveries",
                column: "assigned_driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_assigned_vehicle_id",
                table: "deliveries",
                column: "assigned_vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_status",
                table: "deliveries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_deliveries_tracking_code",
                table: "deliveries",
                column: "tracking_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drivers_license_number",
                table: "drivers",
                column: "license_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maintenances_vehicle_id",
                table: "maintenances",
                column: "vehicle_id",
                unique: true,
                filter: "status IN ('Scheduled', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_routes_assigned_driver_id",
                table: "routes",
                column: "assigned_driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_assigned_vehicle_id",
                table: "routes",
                column: "assigned_vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_status",
                table: "routes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_current_driver_id",
                table: "vehicles",
                column: "current_driver_id");

            migrationBuilder.CreateIndex(
                name: "uq_vehicles_license_plate",
                table: "vehicles",
                column: "license_plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "maintenances");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "drivers");
        }
    }
}
