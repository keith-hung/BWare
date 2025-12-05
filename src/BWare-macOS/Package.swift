// swift-tools-version: 5.9
// The swift-tools-version declares the minimum version of Swift required to build this package.

import PackageDescription

let package = Package(
    name: "BWare",
    platforms: [
        .macOS(.v12)
    ],
    products: [
        .executable(name: "BWare", targets: ["BWare"])
    ],
    dependencies: [],
    targets: [
        .executableTarget(
            name: "BWare",
            dependencies: [],
            path: "BWare",
            resources: [
                .process("../Resources")
            ]
        ),
        .testTarget(
            name: "BWareTests",
            dependencies: ["BWare"],
            path: "BWareTests"
        )
    ]
)
