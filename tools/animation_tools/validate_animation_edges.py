"""Check every selected source frame and runtime contract against an old manifest."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image
from animation_edge_cleanup import clean_edges
from compile_animation_atlases import read_frames, sha256


def validate(root, baseline):
    old = {e['id']: e for e in json.loads(baseline.read_text('utf-8'))['animations']}
    config = json.loads((root/'config/animation-sources.json').read_text('utf-8'))
    current = json.loads((root/'assets/manifests/animations.json').read_text('utf-8'))['animations']
    report = {'catalogAnimations': len(current), 'logicalFrames': 0, 'changed': []}
    selected = {e['id']: e for e in config['animations'] if e.get('edgeCleanup')}
    for e in current:
        report['logicalFrames'] += len(e['frameDurationsMilliseconds'])
        assert sha256(root/'assets'/e['atlasPath']) == e['atlasSha256'], e['id']
        for key, value in old[e['id']].items():
            if key != 'atlasSha256':
                assert e[key] == value, (e['id'], key)
        if e['id'] not in selected:
            assert e == old[e['id']], ('Unselected asset changed', e['id'])
            continue
        source = selected[e['id']]
        frames, _ = read_frames(root/source['source'], 100)
        if source.get('frameSequence') is not None:
            frames = [frames[i] for i in source['frameSequence']]
        if source.get('maximumFrames', 0) > 0:
            frames = frames[:source['maximumFrames']]
        stats = {'id': e['id'], 'frames': len(frames), 'softenedPixels': 0, 'unmattedPixels': 0}
        for frame in frames:
            before = np.asarray(frame.convert('RGBA'))
            after = np.asarray(clean_edges(frame, source['edgeCleanup']))
            np.testing.assert_array_equal(before[:,:,3] > 0, after[:,:,3] > 0)
            alpha = Image.fromarray(before[:,:,3])
            from PIL import ImageFilter
            interior_filter_size = 5 if source['edgeCleanup'] == 'dark-contour-white-matte-wide' else 3
            interior = np.asarray(alpha.filter(ImageFilter.MinFilter(interior_filter_size))) == 255
            np.testing.assert_array_equal(before[interior], after[interior])
            assert np.all(after[:,:,3] <= before[:,:,3])
            changed_rgb = np.any(before[:,:,:3] != after[:,:,:3], axis=2)
            # Fully transparent RGB is intentionally cleared to prevent WPF
            # interpolation from sampling a hidden white matte. Recomposition
            # is meaningful only for pixels that remain visible.
            visible_changed_rgb = changed_rgb & (before[:,:,3] > 0)
            if np.any(visible_changed_rgb):
                pixels = after[visible_changed_rgb].astype(float)
                recomposed = pixels[:,:3]*pixels[:,3:4]/255+255-pixels[:,3:4]
                assert np.max(abs(recomposed-before[visible_changed_rgb,:3])) <= 1
            if source['edgeCleanup'] not in ('dark-contour-white-matte', 'dark-contour-white-matte-wide'):
                white = np.all(before[:,:,:3] >= 235, axis=2) & (before[:,:,3] > 0)
                np.testing.assert_array_equal(before[white,:3], after[white,:3])
            stats['softenedPixels'] += int(np.sum(after[:,:,3] != before[:,:,3]))
            stats['unmattedPixels'] += int(np.sum(changed_rgb))
        report['changed'].append(stats)
        print(e['id'], 'PASS', flush=True)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = validate(Path.cwd(), args.baseline)
    args.output.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
