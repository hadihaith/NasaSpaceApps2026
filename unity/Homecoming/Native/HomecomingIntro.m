#import <AppKit/AppKit.h>
#import <AVFoundation/AVFoundation.h>

static NSView *surface;
static AVPlayer *player;
static AVPlayerLayer *videoLayer;
static id endObserver;
static BOOL ended;

__attribute__((visibility("default"))) void HomecomingIntroStop(void) {
    [player pause];
    if (endObserver) {
        [NSNotificationCenter.defaultCenter removeObserver:endObserver];
        endObserver = nil;
    }
    [videoLayer removeFromSuperlayer];
    [surface removeFromSuperview];
    videoLayer = nil;
    surface = nil;
    player = nil;
    ended = NO;
}

__attribute__((visibility("default"))) int HomecomingIntroStart(const char *path, int muted) {
    NSWindow *window = NSApp.keyWindow ?: NSApp.mainWindow;
    if (!window.contentView) return 0;
    NSString *file = [NSString stringWithUTF8String:path];
    if (!file || ![NSFileManager.defaultManager isReadableFileAtPath:file]) return -1;

    HomecomingIntroStop();
    NSView *content = window.contentView;
    surface = [[NSView alloc] initWithFrame:content.bounds];
    surface.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
    surface.wantsLayer = YES;
    surface.layer.backgroundColor = NSColor.blackColor.CGColor;
    player = [AVPlayer playerWithURL:[NSURL fileURLWithPath:file]];
    player.muted = muted != 0;
    player.actionAtItemEnd = AVPlayerActionAtItemEndPause;
    videoLayer = [AVPlayerLayer playerLayerWithPlayer:player];
    videoLayer.frame = surface.bounds;
    videoLayer.autoresizingMask = kCALayerWidthSizable | kCALayerHeightSizable;
    videoLayer.videoGravity = AVLayerVideoGravityResizeAspect;
    [surface.layer addSublayer:videoLayer];
    [content addSubview:surface positioned:NSWindowAbove relativeTo:nil];
    endObserver = [NSNotificationCenter.defaultCenter
        addObserverForName:AVPlayerItemDidPlayToEndTimeNotification object:player.currentItem
        queue:NSOperationQueue.mainQueue usingBlock:^(NSNotification *note) { ended = YES; }];
    [player play];
    return 1;
}

// -1 = failed, 0 = waiting for picture, 1 = displaying video, 2 = reached the end.
__attribute__((visibility("default"))) int HomecomingIntroState(void) {
    if (ended) return 2;
    if (player.currentItem.status == AVPlayerItemStatusFailed) return -1;
    return videoLayer.readyForDisplay ? 1 : 0;
}

__attribute__((visibility("default"))) double HomecomingIntroTime(void) {
    return CMTimeGetSeconds(player.currentTime);
}

__attribute__((visibility("default"))) void HomecomingIntroPause(int paused) {
    if (paused) [player pause]; else [player play];
}

__attribute__((visibility("default"))) void HomecomingIntroMute(int muted) {
    player.muted = muted != 0;
}

__attribute__((visibility("default"))) void HomecomingIntroSetVolume(float volume) {
    player.volume = MAX(0.0f, MIN(1.0f, volume));
}
