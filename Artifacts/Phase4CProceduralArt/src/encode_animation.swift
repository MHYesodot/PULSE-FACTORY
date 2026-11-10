import AVFoundation
import CoreGraphics
import CoreVideo
import Foundation
import ImageIO

func fail(_ message: String) -> Never {
    fputs("Animation encode failed: \(message)\n", stderr)
    exit(1)
}

guard CommandLine.arguments.count == 4 else {
    fail("usage: encode_animation.swift <frame-directory> <output.mp4> <fps>")
}

let frameDirectory = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
guard let fps = Int32(CommandLine.arguments[3]), fps > 0 else {
    fail("frame rate must be a positive integer")
}

let files: [URL]
do {
    files = try FileManager.default.contentsOfDirectory(
        at: frameDirectory,
        includingPropertiesForKeys: nil
    )
    .filter { $0.lastPathComponent.hasPrefix("diverter_") && $0.pathExtension == "png" }
    .sorted { $0.lastPathComponent < $1.lastPathComponent }
} catch {
    fail("cannot list rendered frames: \(error)")
}

guard files.count >= 2 else {
    fail("at least two rendered PNG frames are required")
}

func loadImage(_ url: URL) -> CGImage {
    guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        fail("cannot read \(url.lastPathComponent)")
    }
    return image
}

let firstImage = loadImage(files[0])
let width = firstImage.width
let height = firstImage.height
guard files.allSatisfy({ loadImage($0).width == width && loadImage($0).height == height }) else {
    fail("all frame dimensions must match")
}

try? FileManager.default.removeItem(at: outputURL)
let writer: AVAssetWriter
do {
    writer = try AVAssetWriter(outputURL: outputURL, fileType: .mp4)
} catch {
    fail("cannot create MP4 writer: \(error)")
}

let input = AVAssetWriterInput(
    mediaType: .video,
    outputSettings: [
        AVVideoCodecKey: AVVideoCodecType.h264,
        AVVideoWidthKey: width,
        AVVideoHeightKey: height,
        AVVideoCompressionPropertiesKey: [
            AVVideoAverageBitRateKey: 2_500_000,
            AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
        ],
    ]
)
input.expectsMediaDataInRealTime = false

let attributes: [String: Any] = [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
    kCVPixelBufferWidthKey as String: width,
    kCVPixelBufferHeightKey as String: height,
    kCVPixelBufferCGImageCompatibilityKey as String: true,
    kCVPixelBufferCGBitmapContextCompatibilityKey as String: true,
]
let adaptor = AVAssetWriterInputPixelBufferAdaptor(
    assetWriterInput: input,
    sourcePixelBufferAttributes: attributes
)

guard writer.canAdd(input) else {
    fail("H.264 writer input is not supported")
}
writer.add(input)
guard writer.startWriting() else {
    fail("cannot start MP4 writer: \(String(describing: writer.error))")
}
writer.startSession(atSourceTime: .zero)

let colorSpace = CGColorSpaceCreateDeviceRGB()
for (index, file) in files.enumerated() {
    while !input.isReadyForMoreMediaData {
        Thread.sleep(forTimeInterval: 0.005)
        if writer.status == .failed {
            fail("MP4 writer failed: \(String(describing: writer.error))")
        }
    }
    var pixelBuffer: CVPixelBuffer?
    let status = CVPixelBufferPoolCreatePixelBuffer(
        kCFAllocatorDefault,
        adaptor.pixelBufferPool!,
        &pixelBuffer
    )
    guard status == kCVReturnSuccess, let buffer = pixelBuffer else {
        fail("cannot allocate a video frame buffer")
    }

    CVPixelBufferLockBaseAddress(buffer, [])
    guard let baseAddress = CVPixelBufferGetBaseAddress(buffer) else {
        CVPixelBufferUnlockBaseAddress(buffer, [])
        fail("cannot access a video frame buffer")
    }
    let bitmapInfo = CGBitmapInfo.byteOrder32Big.rawValue
        | CGImageAlphaInfo.noneSkipFirst.rawValue
    guard let context = CGContext(
        data: baseAddress,
        width: width,
        height: height,
        bitsPerComponent: 8,
        bytesPerRow: CVPixelBufferGetBytesPerRow(buffer),
        space: colorSpace,
        bitmapInfo: bitmapInfo
    ) else {
        CVPixelBufferUnlockBaseAddress(buffer, [])
        fail("cannot create a bitmap context")
    }
    context.draw(loadImage(file), in: CGRect(x: 0, y: 0, width: width, height: height))
    CVPixelBufferUnlockBaseAddress(buffer, [])

    let presentationTime = CMTime(value: Int64(index), timescale: fps)
    guard adaptor.append(buffer, withPresentationTime: presentationTime) else {
        fail("cannot append frame \(index): \(String(describing: writer.error))")
    }
}

input.markAsFinished()
let finished = DispatchSemaphore(value: 0)
writer.finishWriting { finished.signal() }
finished.wait()
guard writer.status == .completed else {
    fail("MP4 finalization failed: \(String(describing: writer.error))")
}
print("Encoded \(files.count) frames at \(fps) fps to \(outputURL.path)")
