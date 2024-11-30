import gulp from 'gulp';
import rev from 'gulp-rev';
import revReplace from 'gulp-rev-replace';
import through from 'through2';
import { deleteAsync } from 'del';

const paths = {
    css: 'src/Angor/Client/wwwroot/assets/css/*.css',
    js: 'src/Angor/Client/wwwroot/assets/js/*.js',
    html: 'src/Angor/Client/wwwroot/index.html',
};

// Task to clean only non-hashed files
gulp.task('clean', async () => {
    return deleteAsync([
        'src/Angor/Client/wwwroot/assets/css/*.css',
        'src/Angor/Client/wwwroot/assets/js/*.js',
        '!src/Angor/Client/wwwroot/assets/css/*-*.css',
        '!src/Angor/Client/wwwroot/assets/js/*-*.js',
    ]);
});

// Task to force unique hashes by appending a timestamp
function addTimestamp() {
    return through.obj(function (file, _, cb) {
        const timestamp = new Date().getTime();
        const appendText = `/* Timestamp: ${timestamp} */\n`;

        if (file.isBuffer()) {
            file.contents = Buffer.concat([Buffer.from(appendText), file.contents]);
        }

        this.push(file);
        cb();
    });
}

// Task to add revision hashes to original files only
gulp.task('revision', () => {
    return gulp.src(
        [
            'src/Angor/Client/wwwroot/assets/css/*.css',
            'src/Angor/Client/wwwroot/assets/js/*.js',
            '!src/Angor/Client/wwwroot/assets/css/*-*.css',
            '!src/Angor/Client/wwwroot/assets/js/*-*.js',
        ],
        { base: 'src/Angor/Client/wwwroot' }
    )
        .pipe(addTimestamp()) // Add timestamp to force hash changes
        .pipe(rev())
        .pipe(gulp.dest('src/Angor/Client/wwwroot'))
        .pipe(rev.manifest())
        .pipe(gulp.dest('src/Angor/Client/wwwroot'));
});

// Task to replace references in HTML
gulp.task('revreplace', () => {
    return gulp.src(paths.html)
        .pipe(
            revReplace({
                manifest: gulp.src('src/Angor/Client/wwwroot/rev-manifest.json'),
            })
        )
        .pipe(gulp.dest('src/Angor/Client/wwwroot'));
});

// Default task: Run tasks in sequence
gulp.task('default', gulp.series('clean', 'revision', 'revreplace'));
